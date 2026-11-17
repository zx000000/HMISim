using System.Globalization;
using HMISim.DriverSdk;

namespace HMISim.Core;

/// <summary>
/// 把界面输入的文本按点位数据类型转换为可写入的值（按钮写入值、数值输入框共用）。
/// 纯函数、无状态，便于单测；转换规则在此一处定义，避免各控件各写一套。
/// </summary>
public static class PointValueParser
{
    /// <summary>尝试转换。失败时 value 为 null、error 为面向操作员的中文原因。</summary>
    public static bool TryParse(string text, TagDataType type, out object? value, out string error)
    {
        value = null;
        error = string.Empty;
        var trimmed = text.Trim();
        if (trimmed.Length == 0 && type != TagDataType.String)
        {
            error = "输入为空";
            return false;
        }

        switch (type)
        {
            case TagDataType.Bool:
                switch (trimmed.ToLowerInvariant())
                {
                    case "1" or "true" or "on" or "开" or "是":
                        value = true;
                        return true;
                    case "0" or "false" or "off" or "关" or "否":
                        value = false;
                        return true;
                    default:
                        error = $"「{text}」不是合法的开关量（0/1/true/false/开/关）";
                        return false;
                }

            case TagDataType.UInt16:
                if (ushort.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u16))
                {
                    value = u16;
                    return true;
                }

                error = $"「{text}」应为 0~65535 的整数";
                return false;

            case TagDataType.Int32:
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i32))
                {
                    value = i32;
                    return true;
                }

                error = $"「{text}」应为整数";
                return false;

            case TagDataType.Float32:
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var f64)
                    || double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out f64))
                {
                    value = (float)f64;
                    return true;
                }

                error = $"「{text}」应为数值";
                return false;

            case TagDataType.String:
                value = text;
                return true;

            default:
                error = $"未知数据类型 {type}";
                return false;
        }
    }
}
