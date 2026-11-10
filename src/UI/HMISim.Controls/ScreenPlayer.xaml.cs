using System.Windows;
using System.Windows.Controls;
using HMISim.Controls.ViewModels;
using HMISim.ProjectModel;

namespace HMISim.Controls;

/// <summary>
/// 画面播放器控件：给它一份工程文档即可播放（自启运行时与刷新节拍，卸载时自动停机）。
/// 使用方式：宿主在代码中设置 <see cref="Project"/>（不要用 XAML 绑定，本控件会接管自身 DataContext）。
/// 组态端预览与运行端共用，保证编辑所见即运行所得。
/// </summary>
public partial class ScreenPlayer : UserControl
{
    /// <summary>工程文档依赖属性。</summary>
    public static readonly DependencyProperty ProjectProperty = DependencyProperty.Register(
        nameof(Project), typeof(ProjectDocument), typeof(ScreenPlayer),
        new PropertyMetadata(null, OnProjectChanged));

    private ScreenPlayerViewModel? _viewModel;

    /// <summary>创建播放器。</summary>
    public ScreenPlayer()
    {
        InitializeComponent();
        Loaded += (_, _) => _viewModel?.Start();
        Unloaded += async (_, _) =>
        {
            if (_viewModel is not null)
                await _viewModel.StopAsync();
        };
    }

    /// <summary>要播放的工程文档。</summary>
    public ProjectDocument? Project
    {
        get => (ProjectDocument?)GetValue(ProjectProperty);
        set => SetValue(ProjectProperty, value);
    }

    private static void OnProjectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ScreenPlayer)d).Rebuild();

    private void Rebuild()
    {
        if (Project is null)
        {
            _viewModel = null;
            DataContext = null;
            return;
        }

        _viewModel = new ScreenPlayerViewModel(Project);
        DataContext = _viewModel;
        if (IsLoaded)
            _viewModel.Start();
    }
}
