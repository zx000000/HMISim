using System.Net;
using System.Net.Sockets;
using HMISim.Common;
using HMISim.DriverSdk;

namespace HMISim.Drivers.Fins;

/// <summary>
/// FINS/UDP 会话：持有 UDP 通道，按合块规划执行读写。
/// UDP 无连接语义：ConnectAsync 打开本地端口；通讯失败在读写结果中以 Fail 返回，
/// 同时把 IsConnected 置假，调度器下一轮会重新 ConnectAsync（重建通道）。
/// </summary>
public sealed class FinsUdpSession : IDeviceSession
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _localPort;
    private readonly int _timeoutMs;
    private UdpClient? _udp;
    private IPEndPoint? _remote;
    private int _sid;
    private bool _disposed;

    /// <summary>按设备配置创建（参数键见 <see cref="FinsDriver.Parameters"/>）。</summary>
    public FinsUdpSession(DeviceConfig config)
    {
        _host = config.GetString("Host", "127.0.0.1");
        _port = config.GetInt("Port", 9600);
        _localPort = config.GetInt("LocalPort", 9601);
        _timeoutMs = config.GetInt("TimeoutMs", 1000);
    }

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _udp?.Dispose();
        _udp = new UdpClient(_localPort);
        if (OperatingSystem.IsWindows())
        {
            // 收到 ICMP 端口不可达时不抛 ConnectionReset，让超时逻辑正常裁定（SnCollector 加固经验）
            _udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
        }

        _remote = new IPEndPoint(IPAddress.Parse(_host), _port);
        IsConnected = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IPointAddress ParseAddress(string raw) => FinsAddress.Parse(raw);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PointValue>> ReadAsync(IReadOnlyList<ReadPoint> points, CancellationToken cancellationToken)
    {
        var results = new List<PointValue>(points.Count);
        foreach (var block in FinsBlockPlanner.Plan(points))
        {
            switch (block)
            {
                case WordBlock wordBlock:
                    results.AddRange(await ReadWordBlockAsync(wordBlock, cancellationToken));
                    break;
                case BitBlock bitBlock:
                    results.Add(await ReadBitAsync(bitBlock.Point, cancellationToken));
                    break;
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PointValue>> WriteAsync(IReadOnlyList<WritePoint> points, CancellationToken cancellationToken)
    {
        var results = new List<PointValue>(points.Count);
        foreach (var point in points)
        {
            var address = (FinsAddress)point.Address;
            try
            {
                byte[] command;
                if (point.DataType == TagDataType.Bool || address.IsBit)
                {
                    command = FinsCodec.BuildWriteBitCommand(address, Convert.ToBoolean(point.Value));
                }
                else
                {
                    var words = FinsCodec.EncodeValue(point.Value, point.DataType, point.LengthWords);
                    command = FinsCodec.BuildWriteWordsCommand(address.Word, words);
                }

                await SendReceiveAsync(command, cancellationToken);
                results.Add(PointValue.Ok(point.PointId, point.Value));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MarkTransportBroken(ex);
                results.Add(PointValue.Fail(point.PointId, $"写 {address.Raw} 失败: {ex.Message}"));
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<PointValue>> ReadWordBlockAsync(WordBlock block, CancellationToken cancellationToken)
    {
        try
        {
            var data = await SendReceiveAsync(FinsCodec.BuildReadWordsCommand(block.StartWord, block.WordCount), cancellationToken);
            var words = FinsCodec.BytesToWords(data, block.WordCount);
            var results = new List<PointValue>(block.Items.Count);
            foreach (var item in block.Items)
            {
                var span = FinsCodec.SpanWords(item.Point.DataType, item.Point.LengthWords);
                var slice = new ushort[span];
                Array.Copy(words, item.OffsetWords, slice, 0, span);
                results.Add(PointValue.Ok(item.Point.PointId, FinsCodec.DecodeValue(slice, item.Point.DataType)));
            }

            return results;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MarkTransportBroken(ex);
            return block.Items
                .Select(i => PointValue.Fail(i.Point.PointId, $"读块 D{block.StartWord}({block.WordCount}字) 失败: {ex.Message}"))
                .ToList();
        }
    }

    private async Task<PointValue> ReadBitAsync(ReadPoint point, CancellationToken cancellationToken)
    {
        var address = (FinsAddress)point.Address;
        try
        {
            var data = await SendReceiveAsync(FinsCodec.BuildReadBitCommand(address), cancellationToken);
            return PointValue.Ok(point.PointId, data.Length > 0 && data[0] != 0x00);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MarkTransportBroken(ex);
            return PointValue.Fail(point.PointId, $"读位 {address.Raw} 失败: {ex.Message}");
        }
    }

    private async Task<byte[]> SendReceiveAsync(byte[] command, CancellationToken cancellationToken)
    {
        if (_udp is null || _remote is null)
            throw new InvalidOperationException("会话未连接");
        var sid = Interlocked.Increment(ref _sid) & 0xFF;
        var packet = FinsCodec.BuildPacket(sid, command);
        await _udp.SendAsync(packet, _remote, cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeoutMs);
        try
        {
            var received = await _udp.ReceiveAsync(timeout.Token);
            return FinsCodec.ParseResponseData(received.Buffer);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"FINS 通讯超时（{_timeoutMs}ms），目标 {_host}:{_port}");
        }
    }

    private void MarkTransportBroken(Exception ex)
    {
        // 只有硬性 socket 错误才判通道损坏；超时只是设备沉默——UDP 无连接可重连，
        // 保留通道、由调度器的失败计数驱动质量降级即可（否则断线场景会重连空转）。
        if (ex is SocketException or ObjectDisposedException)
            IsConnected = false;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        IsConnected = false;
        _udp?.Dispose();
        _udp = null;
        return ValueTask.CompletedTask;
    }
}
