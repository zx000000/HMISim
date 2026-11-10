using System.Collections.ObjectModel;
using System.Windows.Threading;
using HMISim.Core;
using HMISim.ProjectModel;
using HMISim.Simulators.Fins;

namespace HMISim.Controls.ViewModels;

/// <summary>
/// 画面播放器 ViewModel：按工程文档装配运行时（驱动+调度+实时数据库），
/// 以 200ms 节拍把绑定点位的快照刷到各控件项；工程声明使用内置模拟器时一并启停模拟器。
/// 组态端预览与运行端共用本播放器，保证「编辑所见即运行所得」。
/// </summary>
public sealed class ScreenPlayerViewModel : ObservableObject
{
    private readonly ProjectDocument _project;
    private readonly DispatcherTimer _refreshTimer;
    private RuntimeHost? _host;
    private DeviceRuntime? _device;
    private FinsSimulator? _simulator;
    private string _statusText = "未启动";

    /// <summary>按工程文档创建播放器。</summary>
    public ScreenPlayerViewModel(ProjectDocument project)
    {
        _project = project;
        Items = new ObservableCollection<ControlItemViewModel>(
            project.Screen.Controls.Select(c => new ControlItemViewModel(c)));
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _refreshTimer.Tick += (_, _) => Refresh();
    }

    /// <summary>控件项集合。</summary>
    public ObservableCollection<ControlItemViewModel> Items { get; }

    /// <summary>画面逻辑宽度。</summary>
    public double ScreenWidth => _project.Screen.Width;

    /// <summary>画面逻辑高度。</summary>
    public double ScreenHeight => _project.Screen.Height;

    /// <summary>状态栏文本。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    /// <summary>启动播放（模拟器如需、运行时、刷新节拍）。</summary>
    public void Start()
    {
        if (_host is not null)
            return;
        if (_project.UseEmbeddedSimulator)
        {
            var port = 9600;
            if (_project.Device.Parameters.TryGetValue("Port", out var portText))
                int.TryParse(portText, out port);
            _simulator = new FinsSimulator(port);
            _simulator.Start();
        }

        _host = new RuntimeHost();
        _device = _host.AddDevice(ProjectMapper.MapDevice(_project), ProjectMapper.CreateDriver(_project.Device.DriverId), ProjectMapper.MapPoints(_project));
        _host.Start();
        _refreshTimer.Start();
        StatusText = "播放中";
    }

    /// <summary>停止播放并释放。</summary>
    public async Task StopAsync()
    {
        _refreshTimer.Stop();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host = null;
        }

        _simulator?.Stop();
        _simulator?.Dispose();
        _simulator = null;
        StatusText = "已停止";
    }

    private void Refresh()
    {
        if (_host is null || _device is null)
            return;
        foreach (var item in Items)
        {
            if (item.Item.BindingPointId is { } pointId)
                item.Apply(_host.Database.Get(pointId));
        }

        var lastSuccess = _device.LastSuccessAt is { } t ? t.ToString("HH:mm:ss") : "—";
        StatusText = $"{_project.Name} ｜ 设备 {_device.DeviceId} ｜ 成功帧 {_device.SuccessfulFrames} ｜ 失败帧 {_device.FailedFrames} ｜ 最近成功 {lastSuccess}";
    }
}
