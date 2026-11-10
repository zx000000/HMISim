namespace HMISim.ProjectModel;

/// <summary>画面控件种类（阶段 1 最小集，后续按控件清单扩充）。</summary>
public enum ControlKind
{
    /// <summary>数值显示：标题 + 绑定点位的当前值 + 质量呈现（HMISim.Controls.ValueDisplay）。</summary>
    ValueDisplay,

    /// <summary>文本标签：静态文本（标题字段即文本内容）。</summary>
    Label,
}

/// <summary>
/// 画面上的一个控件项（纯数据，组态端编辑、运行端渲染共用）。
/// 坐标为画面逻辑坐标（左上原点，单位像素）。
/// </summary>
public sealed class ControlItem
{
    /// <summary>控件唯一 Id（新建时生成 GUID）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>控件种类。</summary>
    public ControlKind Kind { get; set; } = ControlKind.ValueDisplay;

    /// <summary>标题（ValueDisplay 的标题 / Label 的文本内容）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>绑定的点位 Id（Label 可为空）。</summary>
    public string? BindingPointId { get; set; }

    /// <summary>左上角 X。</summary>
    public double X { get; set; }

    /// <summary>左上角 Y。</summary>
    public double Y { get; set; }

    /// <summary>宽度。</summary>
    public double Width { get; set; } = 240;

    /// <summary>高度。</summary>
    public double Height { get; set; } = 64;

    /// <summary>深拷贝（组态端复制/新建样例时用）。</summary>
    public ControlItem Clone() => new()
    {
        Id = Id,
        Kind = Kind,
        Title = Title,
        BindingPointId = BindingPointId,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
    };
}

/// <summary>一张画面：逻辑尺寸 + 控件集合。</summary>
public sealed class ScreenDefinition
{
    /// <summary>画面名。</summary>
    public string Name { get; set; } = "主画面";

    /// <summary>画面逻辑宽度。</summary>
    public double Width { get; set; } = 960;

    /// <summary>画面逻辑高度。</summary>
    public double Height { get; set; } = 600;

    /// <summary>控件集合（顺序即层叠顺序，后者盖前者）。</summary>
    public List<ControlItem> Controls { get; set; } = [];
}

/// <summary>设备定义（工程内一台设备）。</summary>
public sealed class DeviceDefinition
{
    /// <summary>设备 Id（点位以此归属）。</summary>
    public string DeviceId { get; set; } = "PLC1";

    /// <summary>驱动 Id（如 "fins"）。</summary>
    public string DriverId { get; set; } = "fins";

    /// <summary>驱动参数（字符串字典；数值参数以字符串存、运行时按参数声明解析，避免 JSON 数字/文本往返歧义）。</summary>
    public Dictionary<string, string> Parameters { get; set; } = [];
}

/// <summary>点位定义（工程侧 DTO；运行时由播放器映射为内核的 PointDefinition）。</summary>
public sealed class PointDto
{
    /// <summary>点位 Id（画面绑定以此引用）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>所属设备 Id。</summary>
    public string DeviceId { get; set; } = "PLC1";

    /// <summary>定位串（地址或符号路径）。</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>数据类型名（与内核 TagDataType 枚举同名）。</summary>
    public string DataType { get; set; } = "UInt16";

    /// <summary>缺省采集周期（毫秒）。</summary>
    public int ScanPeriodMs { get; set; } = 500;

    /// <summary>字符串类型字数；其余类型忽略。</summary>
    public int LengthWords { get; set; } = 1;
}

/// <summary>
/// 工程文档：一份工程 = 设备 + 点位表 + 一张画面（阶段 1 最小集；多画面/多设备在后续阶段扩）。
/// JSON 序列化，顶层带 SchemaVersion，只向前兼容（规范 v0.2 第 6 节）。
/// </summary>
public sealed class ProjectDocument
{
    /// <summary>当前工程格式版本。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>格式版本。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>工程名。</summary>
    public string Name { get; set; } = "未命名工程";

    /// <summary>打开本工程所需的最低运行端版本（四方兼容矩阵的工程侧记录）。</summary>
    public string MinRuntimeVersion { get; set; } = "0.1.0";

    /// <summary>是否使用内置模拟器作数据源（演示/离线调试用；接真实 PLC 时关）。</summary>
    public bool UseEmbeddedSimulator { get; set; } = true;

    /// <summary>设备（阶段 1 单设备）。</summary>
    public DeviceDefinition Device { get; set; } = new();

    /// <summary>点位表。</summary>
    public List<PointDto> Points { get; set; } = [];

    /// <summary>画面（阶段 1 单画面）。</summary>
    public ScreenDefinition Screen { get; set; } = new();
}
