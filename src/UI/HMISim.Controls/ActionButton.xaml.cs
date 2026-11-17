using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HMISim.Common;
using HMISim.Controls.ViewModels;
using HMISim.ProjectModel;

namespace HMISim.Controls;

/// <summary>
/// 按钮控件：标题即按钮文字；通讯质量 Bad 时边框变红提示。
/// 点动模式走按下/松开两段写入，其余模式走 Click 一次写入——分发逻辑在
/// <see cref="ControlItemViewModel.PressButton"/> / <see cref="ControlItemViewModel.ReleaseButton"/>。
/// </summary>
public partial class ActionButton : UserControl
{
    private static readonly Brush NormalFrameBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD0, 0xD4, 0xD8));
    private static readonly Brush BadFrameBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x3B, 0x3B));

    /// <summary>标题依赖属性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ActionButton),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    /// <summary>质量码依赖属性。</summary>
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register(
        nameof(Quality), typeof(Quality), typeof(ActionButton),
        new PropertyMetadata(Quality.NoData, OnDisplayPropertyChanged));

    /// <summary>创建控件。</summary>
    public ActionButton()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>按钮文字。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>当前质量码（Bad 时边框变红）。</summary>
    public Quality Quality
    {
        get => (Quality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    private ControlItemViewModel? ViewModel => DataContext as ControlItemViewModel;

    private void InnerButton_OnPreviewDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is { } vm && vm.Item.ActionMode == ButtonActionMode.Momentary)
            vm.PressButton();
    }

    private void InnerButton_OnPreviewUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is { } vm && vm.Item.ActionMode == ButtonActionMode.Momentary)
            vm.ReleaseButton();
    }

    private void InnerButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && vm.Item.ActionMode != ButtonActionMode.Momentary)
            vm.PressButton();
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ActionButton)d).Refresh();

    private void Refresh()
    {
        InnerButton.Content = Title;
        Frame.BorderBrush = Quality == Quality.Bad ? BadFrameBrush : NormalFrameBrush;
    }
}
