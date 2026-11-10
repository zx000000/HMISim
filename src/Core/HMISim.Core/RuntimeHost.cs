using HMISim.Common;
using HMISim.DriverSdk;

namespace HMISim.Core;

/// <summary>
/// 运行时宿主：按工程定义装配全部设备（驱动会话 + 调度循环），统一启停。
/// 组态端「在线模拟」与运行端共用此宿主。
/// </summary>
public sealed class RuntimeHost
{
    private readonly RealtimeDatabase _database = new();
    private readonly DemandRegistry _registry = new();
    private readonly IAppLogger _logger;
    private readonly List<DeviceRuntime> _devices = [];
    private readonly List<Task> _loops = [];
    private CancellationTokenSource? _stopSource;

    /// <summary>创建宿主。</summary>
    public RuntimeHost(IAppLogger? logger = null) => _logger = logger ?? NullLogger.Instance;

    /// <summary>实时数据库（画面/服务层从此订阅）。</summary>
    public RealtimeDatabase Database => _database;

    /// <summary>需求注册表（画面打开时登记加速需求、关闭时按来源注销）。</summary>
    public DemandRegistry Registry => _registry;

    /// <summary>已装配的设备运行时。</summary>
    public IReadOnlyList<DeviceRuntime> Devices => _devices;

    /// <summary>
    /// 装配一台设备：按驱动建会话、建调度运行时，并把点位以缺省周期登记为 "runtime" 常驻需求
    /// （对应周期采集组；画面/报警等来源可再叠加更短周期）。
    /// </summary>
    public DeviceRuntime AddDevice(DeviceConfig config, IDeviceDriver driver, IReadOnlyList<PointDefinition> points)
    {
        var session = driver.CreateSession(config);
        var runtime = new DeviceRuntime(config.DeviceId, points, session, _database, _registry, _logger);
        _devices.Add(runtime);
        foreach (var point in points)
            _registry.Register("runtime", point.PointId, point.ScanPeriod);
        return runtime;
    }

    /// <summary>启动全部设备调度循环。</summary>
    public void Start()
    {
        _stopSource = new CancellationTokenSource();
        foreach (var device in _devices)
            _loops.Add(Task.Run(() => device.RunAsync(_stopSource.Token)));
        _logger.Info($"运行时已启动，设备 {_devices.Count} 台");
    }

    /// <summary>停止全部调度循环并等待退出。</summary>
    public async Task StopAsync()
    {
        _stopSource?.Cancel();
        try
        {
            await Task.WhenAll(_loops);
        }
        catch (OperationCanceledException)
        {
            // 正常停机路径
        }

        _loops.Clear();
        _logger.Info("运行时已停止");
    }
}
