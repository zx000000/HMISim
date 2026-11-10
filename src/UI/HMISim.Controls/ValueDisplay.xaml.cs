using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HMISim.Common;

namespace HMISim.Controls;

/// <summary>
/// 数值显示控件：绑定标题、显示值与质量码三样东西即可工作。
/// 质量变色规则：Good 绿点正常色值；Uncertain 琥珀点、值保持；Bad 红点、值变红（冻结值）；
/// NoData 灰点、占位符。颜色与文案统一取自 <see cref="QualityPresentation"/>。
/// </summary>
public partial class ValueDisplay : UserControl
{
    private static readonly Brush NormalValueBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1F, 0x29, 0x37));
    private static readonly Brush BadValueBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x3B, 0x3B));
    private static readonly Brush NoDataValueBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA0, 0xA6));
    private static readonly BrushConverter BrushConverter = new();

    /// <summary>标题依赖属性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ValueDisplay),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    /// <summary>显示值依赖属性（已格式化文本）。</summary>
    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(string), typeof(ValueDisplay),
        new PropertyMetadata(ValueFormatter.NoValuePlaceholder, OnDisplayPropertyChanged));

    /// <summary>质量码依赖属性。</summary>
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register(
        nameof(Quality), typeof(Quality), typeof(ValueDisplay),
        new PropertyMetadata(Quality.NoData, OnDisplayPropertyChanged));

    /// <summary>创建控件。</summary>
    public ValueDisplay()
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

    /// <summary>当前质量码，驱动圆点颜色与值的前景色。</summary>
    public Quality Quality
    {
        get => (Quality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ValueDisplay)d).Refresh();

    private void Refresh()
    {
        TitleText.Text = Title;
        ValueTextBlock.Text = DisplayValue;
        QualityText.Text = QualityPresentation.Label(Quality);
        QualityDot.Fill = (Brush)BrushConverter.ConvertFromString(QualityPresentation.ColorHex(Quality))!;
        ValueTextBlock.Foreground = Quality switch
        {
            Quality.Bad => BadValueBrush,
            Quality.NoData => NoDataValueBrush,
            _ => NormalValueBrush,
        };
    }
}
