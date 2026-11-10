using System.Globalization;

namespace HMISim.Common;

/// <summary>
/// 质量码的呈现规则（与具体 UI 框架无关的纯逻辑）：标签文案与指示色。
/// 画面表现约定（规范 v0.2 第 4 节）：Good 正常、Uncertain 琥珀提示、Bad 红色冻结、NoData 灰色占位。
/// </summary>
public static class QualityPresentation
{
    /// <summary>质量码的显示文案。</summary>
    public static string Label(Quality quality) => quality switch
    {
        Quality.Good => "正常",
        Quality.Uncertain => "不稳定",
        Quality.Bad => "通讯异常",
        Quality.NoData => "无数据",
        _ => "未知",
    };

    /// <summary>质量指示色（#AARRGGBB 十六进制字符串，UI 层转画刷）。</summary>
    public static string ColorHex(Quality quality) => quality switch
    {
        Quality.Good => "#FF2E9E4F",
        Quality.Uncertain => "#FFE8A33D",
        Quality.Bad => "#FFD93B3B",
        Quality.NoData => "#FF9AA0A6",
        _ => "#FF9AA0A6",
    };
}

/// <summary>
/// 变量值的统一文本格式化（纯逻辑）：浮点两位小数、布尔为 开/关、无值占位。
/// </summary>
public static class ValueFormatter
{
    /// <summary>无数据时显示的占位符。</summary>
    public const string NoValuePlaceholder = "--";

    /// <summary>把快照格式化为显示文本；质量为 NoData 或值为空时返回占位符。</summary>
    public static string Format(ValueSnapshot snapshot)
    {
        if (snapshot.Quality == Quality.NoData || snapshot.Value is null)
            return NoValuePlaceholder;
        return FormatValue(snapshot.Value);
    }

    /// <summary>把单个 CLR 值格式化为显示文本。</summary>
    public static string FormatValue(object value) => value switch
    {
        bool b => b ? "开" : "关",
        float f => f.ToString("F2", CultureInfo.InvariantCulture),
        double d => d.ToString("F2", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? NoValuePlaceholder,
    };
}
