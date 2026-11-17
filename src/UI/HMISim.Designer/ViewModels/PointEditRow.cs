namespace HMISim.Designer.ViewModels;

/// <summary>
/// 点位编辑窗口表格的一行（编辑缓冲）。
/// 数值字段以字符串承载：表格里先自由输入、点「确定」时统一解析校验，
/// 避免 DataGrid 单元格级别的解析报错打断编辑。
/// </summary>
public sealed class PointEditRow
{
    /// <summary>原始点位 Id（新建行为 null）；用于识别改名（画面绑定跟随）与删除（绑定清理）。</summary>
    public string? OriginalId { get; init; }

    /// <summary>点位名称（即点位 Id，画面绑定以此引用）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>定位串（地址或符号路径）。</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>数据类型名（见 ProjectValidator.KnownDataTypes）。</summary>
    public string DataType { get; set; } = "UInt16";

    /// <summary>采集周期（毫秒，文本）。</summary>
    public string ScanPeriodMs { get; set; } = "500";

    /// <summary>字符串字数（文本；非 String 类型忽略）。</summary>
    public string LengthWords { get; set; } = "1";
}
