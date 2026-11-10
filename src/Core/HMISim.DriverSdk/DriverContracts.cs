using HMISim.Common;

namespace HMISim.DriverSdk;

/// <summary>变量数据类型（变量层统一枚举，各驱动负责与协议类型映射）。</summary>
public enum TagDataType
{
    /// <summary>布尔（位）。</summary>
    Bool,

    /// <summary>16 位无符号整数（1 个字）。</summary>
    UInt16,

    /// <summary>32 位有符号整数（2 个字，低字在前）。</summary>
    Int32,

    /// <summary>32 位浮点（2 个字，低字在前）。</summary>
    Float32,

    /// <summary>字符串（ASCII，若干字）。</summary>
    String,
}

/// <summary>驱动通讯参数的种类（供组态端自动生成配置页）。</summary>
public enum DriverParamKind
{
    /// <summary>文本。</summary>
    String,

    /// <summary>整数。</summary>
    Int,

    /// <summary>布尔。</summary>
    Bool,

    /// <summary>枚举（选项在说明中给出）。</summary>
    Enum,
}

/// <summary>
/// 驱动自声明的通讯参数（规范 v0.2 第 4 节第 6 条）。
/// </summary>
/// <param name="Key">参数键（配置文件与代码共用，发布后不得改名）。</param>
/// <param name="DisplayName">组态界面显示名。</param>
/// <param name="Kind">参数种类。</param>
/// <param name="DefaultValue">默认值。</param>
/// <param name="Required">是否必填。</param>
public sealed record DriverParameterDef(
    string Key,
    string DisplayName,
    DriverParamKind Kind,
    object? DefaultValue,
    bool Required);

/// <summary>
/// 一台设备的配置：由哪个驱动负责 + 该驱动的参数取值。
/// </summary>
/// <param name="DeviceId">设备唯一标识（工程内）。</param>
/// <param name="DriverId">驱动标识，与 <see cref="IDeviceDriver.DriverId"/> 对应。</param>
/// <param name="Parameters">参数取值（键对应驱动的参数声明）。</param>
public sealed record DeviceConfig(
    string DeviceId,
    string DriverId,
    IReadOnlyDictionary<string, object?> Parameters)
{
    /// <summary>取字符串参数，缺失时返回默认值。</summary>
    public string GetString(string key, string defaultValue) =>
        Parameters.TryGetValue(key, out var v) && v is string s ? s : defaultValue;

    /// <summary>取整数参数，缺失时返回默认值。</summary>
    public int GetInt(string key, int defaultValue) =>
        Parameters.TryGetValue(key, out var v) && v is not null
            ? Convert.ToInt32(v)
            : defaultValue;
}

/// <summary>已解析的点位地址（对变量层不透明，仅所属驱动能解释）。</summary>
public interface IPointAddress
{
    /// <summary>组态时填写的原始定位串。</summary>
    string Raw { get; }
}

/// <summary>一次读取请求中的点位。</summary>
/// <param name="PointId">变量唯一标识。</param>
/// <param name="Address">驱动已解析的地址。</param>
/// <param name="DataType">数据类型。</param>
/// <param name="LengthWords">字符串类型的字数；其余类型忽略。</param>
public sealed record ReadPoint(string PointId, IPointAddress Address, TagDataType DataType, int LengthWords = 1);

/// <summary>一次写入请求中的点位与目标值。</summary>
/// <param name="PointId">变量唯一标识。</param>
/// <param name="Address">驱动已解析的地址。</param>
/// <param name="DataType">数据类型。</param>
/// <param name="Value">要写入的值（CLR 类型须与 DataType 对应）。</param>
/// <param name="LengthWords">字符串类型的字数；其余类型忽略。</param>
public sealed record WritePoint(string PointId, IPointAddress Address, TagDataType DataType, object Value, int LengthWords = 1);

/// <summary>
/// 点位读写结果：通讯失败不抛异常，以质量码 + 错误信息表达（规范 v0.2 第 4 节第 2 条）。
/// </summary>
/// <param name="PointId">变量唯一标识。</param>
/// <param name="Value">读到的值；失败时为 null（调度器保留数据库中的旧值）。</param>
/// <param name="Quality">质量码：会话层只给出 Good 或 Bad 信号，最终质量由调度器按失败计数裁定。</param>
/// <param name="Error">失败原因（含协议错误码等上下文），成功时为 null。</param>
public sealed record PointValue(string PointId, object? Value, Quality Quality, string? Error)
{
    /// <summary>构造成功结果。</summary>
    public static PointValue Ok(string pointId, object? value) => new(pointId, value, Quality.Good, null);

    /// <summary>构造失败结果。</summary>
    public static PointValue Fail(string pointId, string error) => new(pointId, null, Quality.Bad, error);
}

/// <summary>
/// 设备会话：一条已建立（或待建立）的设备通讯通道。
/// 线程契约：宿主保证每台设备单循环串行调用，会话实现内不需要加锁（规范 v0.2 第 4 节第 5 条）。
/// </summary>
public interface IDeviceSession : IAsyncDisposable
{
    /// <summary>建立连接/打开通道。失败抛异常，由调度器按退避策略重试。</summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>是否处于已连接状态。</summary>
    bool IsConnected { get; }

    /// <summary>
    /// 批量读取：入参为本轮到期点位，会话内部按协议帧长自行合块执行。
    /// 通讯失败时对应点位返回 <see cref="PointValue.Fail"/>，不抛异常。
    /// </summary>
    Task<IReadOnlyList<PointValue>> ReadAsync(IReadOnlyList<ReadPoint> points, CancellationToken cancellationToken);

    /// <summary>批量写入：语义同 <see cref="ReadAsync"/>。</summary>
    Task<IReadOnlyList<PointValue>> WriteAsync(IReadOnlyList<WritePoint> points, CancellationToken cancellationToken);

    /// <summary>把组态定位串解析为驱动地址；格式非法抛 <see cref="FormatException"/>（组态期校验用）。</summary>
    IPointAddress ParseAddress(string raw);
}

/// <summary>
/// 设备驱动（一个协议一个实现、一个程序集）。驱动只做编解码与地址解析，不碰调度（规范 v0.2 第 4、6 节）。
/// </summary>
public interface IDeviceDriver
{
    /// <summary>驱动唯一标识（如 "fins"），写入工程文件，发布后不得更改。</summary>
    string DriverId { get; }

    /// <summary>显示名（如 "欧姆龙 FINS (UDP)"）。</summary>
    string DisplayName { get; }

    /// <summary>本驱动的通讯参数声明，组态端据此自动生成配置页。</summary>
    IReadOnlyList<DriverParameterDef> Parameters { get; }

    /// <summary>按设备配置创建会话（不立即连接，连接由调度器调用 <see cref="IDeviceSession.ConnectAsync"/>）。</summary>
    IDeviceSession CreateSession(DeviceConfig config);
}
