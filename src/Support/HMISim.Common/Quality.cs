namespace HMISim.Common;

/// <summary>
/// 变量质量码（四态，与 OPC UA StatusCode 对齐，规范 v0.2 第 4 节）。
/// </summary>
public enum Quality
{
    /// <summary>从未成功读到（初连/未轮到）。</summary>
    NoData = 0,

    /// <summary>曾有值，但已过期或链路不稳（失败未达判坏阈值）。</summary>
    Uncertain = 1,

    /// <summary>按时读到的新鲜值。</summary>
    Good = 2,

    /// <summary>通讯失败达阈值 / 设备离线，值已冻结。</summary>
    Bad = 3,
}

/// <summary>
/// 变量值的不可变快照：跨线程只传快照，天然免锁（规范 v0.2 第 5 节）。
/// </summary>
/// <param name="Value">当前值（CLR 类型由变量的数据类型决定）；无值时为 null。</param>
/// <param name="Timestamp">该值被读到的时间。</param>
/// <param name="Quality">质量码。</param>
public readonly record struct ValueSnapshot(object? Value, DateTimeOffset Timestamp, Quality Quality)
{
    /// <summary>初始快照：无数据。</summary>
    public static ValueSnapshot Empty => new(null, DateTimeOffset.MinValue, Quality.NoData);
}
