using System.Text;
using HMISim.DriverSdk;

namespace HMISim.Drivers.Fins;

/// <summary>
/// FINS/UDP 编解码纯函数集（不依赖 socket，可金帧测试）。
/// 帧 = 10 字节 FINS 头 + 命令；读命令 0101、写命令 0102。
/// 移植自 SnCollector 的 FinsUdpClient（双格式响应兼容一并保留），并补齐字写入与类型编解码。
/// </summary>
public static class FinsCodec
{
    /// <summary>拼完整发送帧：10 字节头（ICF/RSV/GCT/DNA/DA1/DA2/SNA/SA1/SA2/SID）+ 命令。</summary>
    public static byte[] BuildPacket(int sid, byte[] command)
    {
        var packet = new byte[10 + command.Length];
        packet[0] = 0x80;
        packet[2] = 0x02;
        packet[9] = (byte)(sid & 0xFF);
        Array.Copy(command, 0, packet, 10, command.Length);
        return packet;
    }

    /// <summary>读字命令（0101，区代码 0x82）。</summary>
    public static byte[] BuildReadWordsCommand(ushort startWord, int wordCount) =>
    [
        0x01, 0x01, 0x82,
        (byte)(startWord >> 8), (byte)(startWord & 0xFF),
        0x00,
        (byte)(wordCount >> 8), (byte)(wordCount & 0xFF),
    ];

    /// <summary>读位命令（0101，区代码 0x02）。</summary>
    public static byte[] BuildReadBitCommand(FinsAddress address) =>
    [
        0x01, 0x01, 0x02,
        (byte)(address.Word >> 8), (byte)(address.Word & 0xFF),
        address.Bit ?? 0, 0x00, 0x01,
    ];

    /// <summary>写位命令（0102，区代码 0x02）。</summary>
    public static byte[] BuildWriteBitCommand(FinsAddress address, bool value) =>
    [
        0x01, 0x02, 0x02,
        (byte)(address.Word >> 8), (byte)(address.Word & 0xFF),
        address.Bit ?? 0, 0x00, 0x01, value ? (byte)0x01 : (byte)0x00,
    ];

    /// <summary>写字命令（0102，区代码 0x82），数据大端序。</summary>
    public static byte[] BuildWriteWordsCommand(ushort startWord, ushort[] words)
    {
        var command = new byte[8 + words.Length * 2];
        command[0] = 0x01;
        command[1] = 0x02;
        command[2] = 0x82;
        command[3] = (byte)(startWord >> 8);
        command[4] = (byte)(startWord & 0xFF);
        command[5] = 0x00;
        command[6] = (byte)(words.Length >> 8);
        command[7] = (byte)(words.Length & 0xFF);
        for (var i = 0; i < words.Length; i++)
        {
            command[8 + i * 2] = (byte)(words[i] >> 8);
            command[8 + i * 2 + 1] = (byte)(words[i] & 0xFF);
        }

        return command;
    }

    /// <summary>
    /// 解析响应帧并返回数据段，兼容两种布局：
    /// 标准（欧姆龙 PLC / 本项目模拟器）：[10 字节头][MRES][SRES][数据...]；
    /// HSL Server：头后多 2 字节，MRES 在下标 12（位读时 SRES 缺省、数据从 13 开始；字读时 SRES 在 13、数据从 14 开始）。
    /// 结束码非 0 时抛 <see cref="InvalidOperationException"/> 并带 MRES/SRES。
    /// </summary>
    public static byte[] ParseResponseData(byte[] response)
    {
        if (response.Length < 12)
            throw new InvalidOperationException("FINS 响应帧过短");
        if (response[10] == 0x00 && response[11] == 0x00)
            return response[12..];
        if (response.Length >= 14 && response[12] == 0x00)
            return response[13] == 0x00 ? response[14..] : response[13..];
        throw new InvalidOperationException($"FINS 返回错误: MRES={response[10]:X2} SRES={response[11]:X2}");
    }

    /// <summary>数据段字节（大端）转字数组。</summary>
    public static ushort[] BytesToWords(byte[] data, int wordCount)
    {
        if (data.Length < wordCount * 2)
            throw new InvalidOperationException("FINS 返回数据不足");
        var words = new ushort[wordCount];
        for (var i = 0; i < wordCount; i++)
            words[i] = (ushort)((data[i * 2] << 8) | data[i * 2 + 1]);
        return words;
    }

    /// <summary>某数据类型占用的字数。</summary>
    public static int SpanWords(TagDataType dataType, int lengthWords) => dataType switch
    {
        TagDataType.UInt16 => 1,
        TagDataType.Int32 => 2,
        TagDataType.Float32 => 2,
        TagDataType.String => Math.Max(1, lengthWords),
        _ => 1,
    };

    /// <summary>把字数组按数据类型解码为 CLR 值（32 位量低字在前；字符串去尾空白与控制符）。</summary>
    public static object DecodeValue(ushort[] words, TagDataType dataType)
    {
        switch (dataType)
        {
            case TagDataType.UInt16:
                return words[0];
            case TagDataType.Int32:
                return (int)(((uint)words[1] << 16) | words[0]);
            case TagDataType.Float32:
                return BitConverter.UInt32BitsToSingle(((uint)words[1] << 16) | words[0]);
            case TagDataType.String:
                var bytes = new byte[words.Length * 2];
                for (var i = 0; i < words.Length; i++)
                {
                    bytes[i * 2] = (byte)(words[i] >> 8);
                    bytes[i * 2 + 1] = (byte)(words[i] & 0xFF);
                }

                // 扫码类数据常带尾随 \r\n，须一并去除（SnCollector 实测教训）
                return Encoding.ASCII.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "位类型不走字解码");
        }
    }

    /// <summary>把 CLR 值按数据类型编码为字数组（写命令用）。</summary>
    public static ushort[] EncodeValue(object value, TagDataType dataType, int lengthWords)
    {
        switch (dataType)
        {
            case TagDataType.UInt16:
                return [Convert.ToUInt16(value)];
            case TagDataType.Int32:
                var i = Convert.ToInt32(value);
                return [(ushort)(i & 0xFFFF), (ushort)((i >> 16) & 0xFFFF)];
            case TagDataType.Float32:
                var bits = BitConverter.SingleToUInt32Bits(Convert.ToSingle(value));
                return [(ushort)(bits & 0xFFFF), (ushort)((bits >> 16) & 0xFFFF)];
            case TagDataType.String:
                var text = Convert.ToString(value) ?? string.Empty;
                var raw = Encoding.ASCII.GetBytes(text);
                var count = Math.Max(1, lengthWords);
                var words = new ushort[count];
                for (var w = 0; w < count; w++)
                {
                    var hi = w * 2 < raw.Length ? raw[w * 2] : (byte)0;
                    var lo = w * 2 + 1 < raw.Length ? raw[w * 2 + 1] : (byte)0;
                    words[w] = (ushort)((hi << 8) | lo);
                }

                return words;
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "位类型不走字编码");
        }
    }
}
