using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HMISim.Common;

namespace HMISim.Controls;

/// <summary>
/// 指示灯控件：亮 = 绿、灭 = 深灰；质量 Bad 时灯体转浅灰并描红边（坏数据不许冒充「关」），
/// NoData 为浅灰无边。质量文案与 <see cref="ValueDisplay"/> 同源（QualityPresentation）。
/// </summary>
public partial class IndicatorLamp : UserControl
{
    private static readonly Brush OnBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x22, 0xC5, 0x5E));
    private static readonly Brush OffBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4B, 0x55, 0x63));
    private static readonly Brush UnknownBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD1, 0xD5, 0xDB));
    private static readonly Brush BadStrokeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x3B, 0x3B));
    private static readonly Brush NormalStrokeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA0, 0xA6));

    /// <summary>标题依赖属性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(IndicatorLamp),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    /// <summary>亮/灭依赖属性。</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool), typeof(IndicatorLamp),
        new PropertyMetadata(false, OnDisplayPropertyChanged));

    /// <summary>质量码依赖属性。</summary>
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register(
        nameof(Quality), typeof(Quality), typeof(IndicatorLamp),
        new PropertyMetadata(Quality.NoData, OnDisplayPropertyChanged));

    /// <summary>创建控件。</summary>
    public IndicatorLamp()
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

    /// <summary>是否亮灯。</summary>
    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>当前质量码。</summary>
    public Quality Quality
    {
        get => (Quality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IndicatorLamp)d).Refresh();

    private void Refresh()
    {
        TitleText.Text = Title;
        QualityText.Text = QualityPresentation.Label(Quality);
        LampBody.Fill = Quality switch
        {
            Quality.Bad or Quality.NoData => UnknownBrush,
            _ => IsOn ? OnBrush : OffBrush,
        };
        LampBody.Stroke = Quality == Quality.Bad ? BadStrokeBrush : NormalStrokeBrush;
    }
}
