using HMISim.DriverSdk;

namespace HMISim.Drivers.Fins;

/// <summary>
/// FINS 地址解析结果（阶段 0 仅支持 D 区，格式 "D100" 或 "D100.05"）。
/// 来源：移植自 SnCollector 的 FinsUdpClient.ParseAddress 并扩展为结构化地址。
/// </summary>
/// <param name="Word">字地址。</param>
/// <param name="Bit">位号（0~15）；null 表示按字访问。</param>
public sealed record FinsAddress(ushort Word, byte? Bit) : IPointAddress
{
    /// <inheritdoc />
    public string Raw => Bit is null ? $"D{Word}" : $"D{Word}.{Bit.Value:00}";

    /// <summary>是否为位访问。</summary>
    public bool IsBit => Bit is not null;

    /// <summary>FINS 存储区代码：DM 字 = 0x82，DM 位 = 0x02。</summary>
    public byte AreaCode => IsBit ? (byte)0x02 : (byte)0x82;

    /// <summary>解析定位串。格式非法抛 <see cref="FormatException"/>。</summary>
    public static FinsAddress Parse(string raw)
    {
        var text = raw.Trim().ToUpperInvariant();
        if (!text.StartsWith('D') || text.Length < 2)
            throw new FormatException($"不支持的地址: {raw}（当前仅支持 D 区，如 D100 / D100.05）");
        var rest = text[1..];
        byte? bit = null;
        var dot = rest.IndexOf('.');
        if (dot >= 0)
        {
            if (!byte.TryParse(rest[(dot + 1)..], out var b) || b > 15)
                throw new FormatException($"位号非法: {raw}（应为 00~15）");
            bit = b;
            rest = rest[..dot];
        }

        if (!ushort.TryParse(rest, out var word))
            throw new FormatException($"字地址非法: {raw}");
        return new FinsAddress(word, bit);
    }
}
