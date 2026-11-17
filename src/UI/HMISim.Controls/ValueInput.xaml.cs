using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HMISim.Common;
using HMISim.Controls.ViewModels;

namespace HMISim.Controls;

/// <summary>
/// 数值输入控件：点「写入」或在输入框回车，把文本交给
/// <see cref="ControlItemViewModel.SubmitInput"/> 做类型转换与量程校验后写入；
/// 当前值独立显示，不回填输入框（操作员输入中途不被刷新覆盖）。
/// </summary>
public partial class ValueInput : UserControl
{
    /// <summary>标题依赖属性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ValueInput),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    /// <summary>当前值依赖属性（已格式化文本）。</summary>
    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(string), typeof(ValueInput),
        new PropertyMetadata(ValueFormatter.NoValuePlaceholder, OnDisplayPropertyChanged));

    /// <summary>创建控件。</summary>
    public ValueInput()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>标题文本。</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>当前显示值（格式化后的文本）。</summary>
    public string DisplayValue
    {
        get => (string)GetValue(DisplayValueProperty);
        set => SetValue(DisplayValueProperty, value);
    }

    private void WriteButton_OnClick(object sender, RoutedEventArgs e) => Submit();

    private void InputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
        }
    }

    private void Submit()
    {
        if (DataContext is ControlItemViewModel vm)
            vm.SubmitInput(InputBox.Text);
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ValueInput)d).Refresh();

    private void Refresh()
    {
        TitleText.Text = Title;
        ValueRun.Text = DisplayValue;
    }
}
