using System.Threading.Channels;
using HMISim.Common;
using HMISim.DriverSdk;

namespace HMISim.Core;

/// <summary>
/// 单台设备的调度运行时（原型书第 9 节）：一个独立异步循环，
/// 按需求注册表的并集决定读什么——到期计算、写优先、结果回填实时数据库、失败按计数降级质量。
/// 一台设备的故障只影响本循环，不阻塞其他设备。
/// </summary>
public sealed class DeviceRuntime
{
    /// <summary>连续失败达到此次数，质量由 Uncertain 判为 Bad。</summary>
    public const int BadThreshold = 3;

    private static readonly TimeSpan MaxTick = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MinTick = TimeSpan.FromMilliseconds(5);

    private readonly IReadOnlyList<PointDefinition> _points;
    private readonly IReadOnlyDictionary<string, IPointAddress> _addresses;
    private readonly IDeviceSession _session;
    private readonly RealtimeDatabase _database;
    private readonly DemandRegistry _registry;
    private readonly IAppLogger _logger;
    private readonly Channel<WritePoint> _writeQueue;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly Dictionary<string, DateTimeOffset> _nextDue = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _failCounts = new(StringComparer.Ordinal);
    private bool _connectLogPending = true;

    /// <summary>设备 Id。</summary>
    public string DeviceId { get; }

    /// <summary>成功帧数（可观测计数，规范 v0.2 第 12 节）。</summary>
    public long SuccessfulFrames { get; private set; }

    /// <summary>失败帧数（可观测计数）。</summary>
    public long FailedFrames { get; private set; }

    /// <summary>最近一次通讯成功时间。</summary>
    public DateTimeOffset? LastSuccessAt { get; private set; }

    /// <summary>
    /// 创建设备运行时。点位地址在此一次性解析（组态错误在启动期暴露，不拖到运行期）。
    /// </summary>
    public DeviceRuntime(
        string deviceId,
        IReadOnlyList<PointDefinition> points,
        IDeviceSession session,
        RealtimeDatabase database,
        DemandRegistry registry,
        IAppLogger? logger = null)
    {
        DeviceId = deviceId;
        _points = points;
        _session = session;
        _database = database;
        _registry = registry;
        _logger = logger ?? NullLogger.Instance;
        _addresses = points.ToDictionary(p => p.PointId, p => session.ParseAddress(p.Address), StringComparer.Ordinal);
        _writeQueue = Channel.CreateBounded<WritePoint>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        foreach (var point in points)
        {
            _nextDue[point.PointId] = DateTimeOffset.MinValue;
            _failCounts[point.PointId] = 0;
        }
    }

    /// <summary>把一次写请求放入优先队列（满时丢弃最旧，见通道配置）。返回是否入队成功。</summary>
    public bool TryEnqueueWrite(string pointId, object value)
    {
        var point = _points.FirstOrDefault(p => p.PointId == pointId)
            ?? throw new ArgumentException($"未知点位: {pointId}", nameof(pointId));
        var write = new WritePoint(pointId, _addresses[pointId], point.DataType, value, point.LengthWords);
        var queued = _writeQueue.Writer.TryWrite(write);
        if (queued && _wakeSignal.CurrentCount == 0)
            _wakeSignal.Release();
        return queued;
    }

    /// <summary>运行调度循环，直到取消。单次迭代的任何异常都被捕获记录，循环永不自杀（规范 v0.2 第 5 节）。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var backoffMs = 200;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_session.IsConnected)
                {
                    await _session.ConnectAsync(cancellationToken);
                    backoffMs = 200;
                    if (_connectLogPending)
                    {
                        _logger.Info($"设备 {DeviceId} 已连接");
                        _connectLogPending = false;
                    }
                }

                await DrainWritesAsync(cancellationToken);
                var sleep = await PollDuePointsAsync(cancellationToken);
                await WaitTickAsync(sleep, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                FailedFrames++;
                _logger.Error($"设备 {DeviceId} 调度迭代异常，将退避重试", ex);
                MarkDemandedPointsFailed();
                await SafeDelayAsync(backoffMs, cancellationToken);
                backoffMs = Math.Min(backoffMs * 2, 5000);
                // 不在此释放会话：会话自管连接状态（IsConnected），循环下一轮按需重连。
            }
        }
    }

    private async Task DrainWritesAsync(CancellationToken cancellationToken)
    {
        List<WritePoint>? batch = null;
        while (_writeQueue.Reader.TryRead(out var write))
            (batch ??= new List<WritePoint>()).Add(write);
        if (batch is null)
            return;

        var results = await _session.WriteAsync(batch, cancellationToken);
        foreach (var result in results)
        {
            if (result.Quality == Quality.Good)
            {
                SuccessfulFrames++;
                LastSuccessAt = DateTimeOffset.Now;
                _failCounts[result.PointId] = 0;
                // 写成功后立即回填本地值，画面不必等下一轮读
                var write = batch.First(w => w.PointId == result.PointId);
                _database.Update(result.PointId, write.Value, Quality.Good);
            }
            else
            {
                FailedFrames++;
                _logger.Error($"设备 {DeviceId} 写点位 {result.PointId} 失败: {result.Error}");
                RegisterPointFailure(result.PointId, logTransition: false);
            }
        }
    }

    /// <summary>执行一轮到期读取，返回建议的休眠时长（到最近一个到期点）。</summary>
    private async Task<TimeSpan> PollDuePointsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        var due = new List<(PointDefinition Point, TimeSpan Period)>();
        var nextSleep = MaxTick;
        foreach (var point in _points)
        {
            var period = _registry.GetEffectivePeriod(point.PointId);
            if (period is null)
                continue; // 无人需求：不通讯
            if (_nextDue[point.PointId] <= now)
                due.Add((point, period.Value));
            else
            {
                var remain = _nextDue[point.PointId] - now;
                if (remain < nextSleep)
                    nextSleep = remain;
            }
        }

        if (due.Count == 0)
            return nextSleep;

        var readPoints = due
            .Select(d => new ReadPoint(d.Point.PointId, _addresses[d.Point.PointId], d.Point.DataType, d.Point.LengthWords))
            .ToList();
        var results = await _session.ReadAsync(readPoints, cancellationToken);
        var resultByPoint = results.ToDictionary(r => r.PointId, StringComparer.Ordinal);
        foreach (var (point, period) in due)
        {
            _nextDue[point.PointId] = DateTimeOffset.Now + period;
            if (!resultByPoint.TryGetValue(point.PointId, out var result))
            {
                RegisterPointFailure(point.PointId);
                continue;
            }

            if (result.Quality == Quality.Good)
            {
                SuccessfulFrames++;
                LastSuccessAt = DateTimeOffset.Now;
                _failCounts[point.PointId] = 0;
                _database.Update(point.PointId, result.Value, Quality.Good);
            }
            else
            {
                FailedFrames++;
                RegisterPointFailure(point.PointId);
            }
        }

        return MinTick;
    }

    private void RegisterPointFailure(string pointId, bool logTransition = true)
    {
        var count = _failCounts[pointId] + 1;
        _failCounts[pointId] = count;
        _database.UpdateQuality(pointId, count >= BadThreshold ? Quality.Bad : Quality.Uncertain);
        // 失败日志只记状态迁移点（首次失败/判坏），不逐轮刷屏（规范 v0.2 第 7 节）
        if (logTransition && count == 1)
            _logger.Warn($"设备 {DeviceId} 点位 {pointId} 通讯失败（第 1 次），质量降为 Uncertain");
        else if (logTransition && count == BadThreshold)
        {
            _logger.Error($"设备 {DeviceId} 点位 {pointId} 连续失败 {BadThreshold} 次，质量判 Bad");
            _connectLogPending = true; // 恢复连通时再播报一次"已连接"
        }
    }

    private void MarkDemandedPointsFailed()
    {
        foreach (var point in _points)
        {
            if (_registry.GetEffectivePeriod(point.PointId) is not null)
                RegisterPointFailure(point.PointId);
        }
    }

    private async Task WaitTickAsync(TimeSpan sleep, CancellationToken cancellationToken)
    {
        if (sleep < MinTick)
            sleep = MinTick;
        if (sleep > MaxTick)
            sleep = MaxTick;
        try
        {
            await _wakeSignal.WaitAsync(sleep, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 退出循环由 RunAsync 的主条件裁定
        }
    }

    private static async Task SafeDelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(milliseconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 忽略：主循环将退出
        }
    }
}
