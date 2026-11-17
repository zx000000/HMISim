using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HMISim.Common;

namespace HMISim.Controls;

/// <summary>
/// 进度条控件：标题 + 数值 + 水平填充条。填充宽度 = Fraction × 轨道宽度（轨道尺寸变化时重算）；
/// 填充色随质量变化：Good 蓝、Uncertain 琥珀、Bad 红、NoData 灰。
/// </summary>
public partial class LevelBar : UserControl
{
    private static readonly Brush GoodBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB));
    private static readonly Brush UncertainBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x77, 0x06));
    private static readonly Brush BadBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD9, 0x3B, 0x3B));
    private static readonly Brush NoDataBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA0, 0xA6));

    /// <summary>标题依赖属性。</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(LevelBar),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    /// <summary>显示值依赖属性（已格式化文本）。</summary>
    public static readonly DependencyProperty DisplayValueProperty = DependencyProperty.Register(
        nameof(DisplayValue), typeof(string), typeof(LevelBar),
        new PropertyMetadata(ValueFormatter.NoValuePlaceholder, OnDisplayPropertyChanged));

    /// <summary>填充比例依赖属性（0~1）。</summary>
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
        nameof(Fraction), typeof(double), typeof(LevelBar),
        new PropertyMetadata(0d, OnDisplayPropertyChanged));

    /// <summary>质量码依赖属性。</summary>
    public static readonly DependencyProperty QualityProperty = DependencyProperty.Register(
        nameof(Quality), typeof(Quality), typeof(LevelBar),
        new PropertyMetadata(Quality.NoData, OnDisplayPropertyChanged));

    /// <summary>创建控件。</summary>
    public LevelBar()
    {
        InitializeComponent();
        Track.SizeChanged += (_, _) => Refresh();
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

    /// <summary>填充比例（0~1）。</summary>
    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    /// <summary>当前质量码（决定填充色）。</summary>
    public Quality Quality
    {
        get => (Quality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((LevelBar)d).Refresh();

    private void Refresh()
    {
        TitleText.Text = Title;
        ValueTextBlock.Text = DisplayValue;
        Fill.Background = Quality switch
        {
            Quality.Good => GoodBrush,
            Quality.Uncertain => UncertainBrush,
            Quality.Bad => BadBrush,
            _ => NoDataBrush,
        };
        Fill.Width = Math.Max(0, Track.ActualWidth - 2) * Math.Clamp(Fraction, 0, 1);
    }
}
