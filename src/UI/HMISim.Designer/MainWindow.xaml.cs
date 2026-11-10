using System.Windows;
using HMISim.Controls;
using HMISim.Designer.ViewModels;
using HMISim.ProjectModel;
using Microsoft.Win32;

namespace HMISim.Designer;

/// <summary>
/// 组态端主窗口：只做视图装配与对话框/画布事件的接线，文档状态归 <see cref="DesignerViewModel"/>。
/// </summary>
public partial class MainWindow : Window
{
    private readonly DesignerViewModel _viewModel = new();

    /// <summary>创建主窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.DocumentReplaced += () => Editor.SetDocument(_viewModel.Document);
        _viewModel.ItemEdited += item => Editor.RefreshItem(item);
        Editor.SelectionChanged += item => _viewModel.SelectItem(item);
        Editor.GeometryChanged += item =>
        {
            if (item == _viewModel.SelectedItem)
                _viewModel.ReloadBuffer();
        };
        Loaded += (_, _) => Editor.SetDocument(_viewModel.Document);
    }

    private void NewButton_OnClick(object sender, RoutedEventArgs e) => _viewModel.NewDocument();

    private void OpenButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "HMISim 工程 (*.json)|*.json|全部文件 (*.*)|*.*",
            Title = "打开工程",
        };
        if (dialog.ShowDialog() != true)
            return;
        try
        {
            _viewModel.LoadFrom(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开工程失败：{ex.Message}", "HMISim 组态端", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var path = _viewModel.CurrentFilePath;
        if (path is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "HMISim 工程 (*.json)|*.json",
                Title = "保存工程",
                FileName = $"{_viewModel.Document.Name}.json",
            };
            if (dialog.ShowDialog() != true)
                return;
            path = dialog.FileName;
        }

        try
        {
            _viewModel.SaveTo(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "HMISim 组态端", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PreviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        var player = new ScreenPlayer { Project = _viewModel.Document, Margin = new Thickness(10) };
        var window = new Window
        {
            Title = $"预览 — {_viewModel.Document.Name}",
            Content = player,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = System.Windows.Media.Brushes.WhiteSmoke,
        };
        window.Show();
    }

    private void AddValueButton_OnClick(object sender, RoutedEventArgs e) => AddControl(ControlKind.ValueDisplay);

    private void AddLabelButton_OnClick(object sender, RoutedEventArgs e) => AddControl(ControlKind.Label);

    private void AddControl(ControlKind kind)
    {
        var item = _viewModel.AddControl(kind);
        Editor.Rebuild();
        Editor.Select(item);
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e) => _viewModel.DeleteSelected();

    private void ApplyButton_OnClick(object sender, RoutedEventArgs e) => _viewModel.ApplyEdits();
}
