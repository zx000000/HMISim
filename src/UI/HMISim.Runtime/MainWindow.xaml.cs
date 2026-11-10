using System.Windows;
using HMISim.Runtime.ViewModels;

namespace HMISim.Runtime;

/// <summary>
/// 主窗口：只负责视图装配与生命周期（启动/停止 ViewModel），业务逻辑全在 ViewModel 与下层（规范 v0.2 第 9 节）。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    /// <summary>创建主窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Start();
        Closed += async (_, _) => await _viewModel.StopAsync();
    }
}
