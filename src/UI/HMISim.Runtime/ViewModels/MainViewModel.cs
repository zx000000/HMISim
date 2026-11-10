using System.Collections.ObjectModel;
using System.Windows.Threading;
using HMISim.Common;
using HMISim.Core;
using HMISim.DriverSdk;
using HMISim.Drivers.Fins;
using HMISim.Simulators.Fins;

namespace HMISim.Runtime.ViewModels;

/// <summary>
/// 运行端最小壳的主 ViewModel（阶段 0）：装配一台 FINS 设备（演示工程写死，
/// 工程文件驱动留待后续阶段），进程内带 FINS 模拟器作数据源；
/// 以 200ms 节拍从实时数据库批量刷新画面（规范 v0.2 第 5 节的刷新合并节流）。
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly DispatcherTimer _refreshTimer;
    private RuntimeHost? _host;
    private DeviceRuntime? _device;
    private FinsSimulator? _simulator;
    private bool _simulatorRunning;
    private string _setpointInput = "1234";
    private string _statusText = "未启动";
    private string _messageText = string.Empty;

    /// <summary>创建主 ViewModel 并准备演示点位。</summary>
    public MainViewModel()
    {
        Rows = new ObservableCollection<PointRowViewModel>(
            PointDefinitions.Select(p => new PointRowViewModel(p.PointId, p.PointId)));
        WriteSetpointCommand = new RelayCommand(WriteSetpoint);
        DisconnectCommand = new RelayCommand(() => SetSimulatorRunning(false), () => _simulatorRunning);
        ReconnectCommand = new RelayCommand(() => SetSimulatorRunning(true), () => !_simulatorRunning);
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _refreshTimer.Tick += (_, _) => Refresh();
    }

    /// <summary>演示工程点位（与控制台 DemoHost 保持一致）。</summary>
    private static IReadOnlyList<PointDefinition> PointDefinitions { get; } =
    [
        new("计数器", "PLC1", "D0", TagDataType.UInt16, TimeSpan.FromMilliseconds(200)),
        new("温度", "PLC1", "D2", TagDataType.Float32, TimeSpan.FromMilliseconds(500)),
        new("运行信号", "PLC1", "D100.00", TagDataType.Bool, TimeSpan.FromMilliseconds(200)),
        new("设定值", "PLC1", "D200", TagDataType.UInt16, TimeSpan.FromMilliseconds(1000)),
    ];

    /// <summary>点位行集合。</summary>
    public ObservableCollection<PointRowViewModel> Rows { get; }

    /// <summary>设定值输入框文本。</summary>
    public string SetpointInput
    {
        get => _setpointInput;
        set => SetField(ref _setpointInput, value);
    }

    /// <summary>状态栏文本（设备帧计数与最近成功时间）。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>操作反馈文本（写入结果等）。</summary>
    public string MessageText
    {
        get => _messageText;
        private set => SetField(ref _messageText, value);
    }

    /// <summary>写入设定值命令。</summary>
    public RelayCommand WriteSetpointCommand { get; }

    /// <summary>模拟拔线命令（停模拟器）。</summary>
    public RelayCommand DisconnectCommand { get; }

    /// <summary>恢复连接命令（启模拟器）。</summary>
    public RelayCommand ReconnectCommand { get; }

    /// <summary>启动模拟器与运行时（窗口加载时调用）。</summary>
    public void Start()
    {
        _simulator = new FinsSimulator(9600);
        SetSimulatorRunning(true);

        var driver = new FinsDriver();
        var config = new DeviceConfig("PLC1", driver.DriverId, new Dictionary<string, object?>
        {
            ["Host"] = "127.0.0.1",
            ["Port"] = 9600,
            ["LocalPort"] = 9601,
            ["TimeoutMs"] = 300,
        });
        _host = new RuntimeHost(new WindowLogger(message => MessageText = message));
        _device = _host.AddDevice(config, driver, PointDefinitions);
        _host.Start();
        _refreshTimer.Start();
        StatusText = "运行中";
    }

    /// <summary>停止运行时与模拟器（窗口关闭时调用）。</summary>
    public async Task StopAsync()
    {
        _refreshTimer.Stop();
        if (_host is not null)
            await _host.StopAsync();
        SetSimulatorRunning(false);
        _simulator?.Dispose();
    }

    private void SetSimulatorRunning(bool running)
    {
        if (_simulator is null || _simulatorRunning == running)
            return;
        if (running)
            _simulator.Start();
        else
            _simulator.Stop();
        _simulatorRunning = running;
        DisconnectCommand.RaiseCanExecuteChanged();
        ReconnectCommand.RaiseCanExecuteChanged();
        MessageText = running ? "模拟器已启动（已恢复连接）" : "模拟器已停止（模拟拔线，观察质量变色）";
    }

    private void WriteSetpoint()
    {
        if (_device is null)
            return;
        if (!ushort.TryParse(SetpointInput, out var value))
        {
            MessageText = "设定值必须是 0~65535 的整数";
            return;
        }

        var queued = _device.TryEnqueueWrite("设定值", value);
        MessageText = queued ? $"已下发设定值 {value}（写队列优先于读）" : "写队列已满，下发失败";
    }

    private void Refresh()
    {
        if (_host is null || _device is null)
            return;
        foreach (var row in Rows)
            row.Apply(_host.Database.Get(row.PointId));
        var lastSuccess = _device.LastSuccessAt is { } t ? t.ToString("HH:mm:ss") : "—";
        StatusText = $"设备 PLC1 ｜ 成功帧 {_device.SuccessfulFrames} ｜ 失败帧 {_device.FailedFrames} ｜ 最近成功 {lastSuccess}";
    }

    /// <summary>把内核日志的错误概要显示到界面反馈区（最小壳的权宜通道，正式诊断页在后续阶段）。</summary>
    private sealed class WindowLogger(Action<string> onError) : IAppLogger
    {
        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null) => onError(message);
    }
}
