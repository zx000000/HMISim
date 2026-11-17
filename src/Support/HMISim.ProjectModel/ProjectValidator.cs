using System.Globalization;

namespace HMISim.ProjectModel;

/// <summary>
/// 工程文档的协议无关校验（组态端保存/确认编辑前调用；运行端打开工程时亦可复用）。
/// 只查结构性问题：点位 Id 唯一与非空、地址非空、数据类型名合法、周期/字数在界内、
/// 点位归属设备与文档设备一致、控件量程合法、按钮写入值与绑定点位类型相符。
/// 地址格式是否合法属驱动职责，由组态端另行调用驱动
/// ParseAddress 校验，不在本类范围内。
/// </summary>
public static class ProjectValidator
{
    /// <summary>合法的数据类型名（与内核 TagDataType 枚举同名，字符串存工程文件）。</summary>
    public static IReadOnlyList<string> KnownDataTypes { get; } = ["Bool", "UInt16", "Int32", "Float32", "String"];

    /// <summary>采集周期下限（毫秒）：低于此值通讯调度无实际意义且易打满通道。</summary>
    public const int MinScanPeriodMs = 50;

    /// <summary>采集周期上限（毫秒）。</summary>
    public const int MaxScanPeriodMs = 60000;

    /// <summary>字符串点位字数上限。</summary>
    public const int MaxLengthWords = 256;

    /// <summary>校验整份文档，返回问题清单（空清单 = 通过）。</summary>
    public static IReadOnlyList<string> Validate(ProjectDocument document)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(document.Device.DeviceId))
            issues.Add("设备 Id 不能为空");
        if (string.IsNullOrWhiteSpace(document.Device.DriverId))
            issues.Add("驱动 Id 不能为空");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var point in document.Points)
        {
            var label = string.IsNullOrWhiteSpace(point.Id) ? "（未命名点位）" : $"点位「{point.Id}」";
            if (string.IsNullOrWhiteSpace(point.Id))
                issues.Add($"{label}：名称不能为空");
            else if (!seen.Add(point.Id))
                issues.Add($"{label}：名称重复（画面绑定按名称引用，必须唯一）");
            if (!string.IsNullOrWhiteSpace(document.Device.DeviceId)
                && !string.Equals(point.DeviceId, document.Device.DeviceId, StringComparison.Ordinal))
                issues.Add($"{label}：所属设备「{point.DeviceId}」与工程设备「{document.Device.DeviceId}」不一致");
            if (string.IsNullOrWhiteSpace(point.Address))
                issues.Add($"{label}：地址不能为空");
            if (!KnownDataTypes.Contains(point.DataType))
                issues.Add($"{label}：数据类型「{point.DataType}」不合法（可选 {string.Join("/", KnownDataTypes)}）");
            if (point.ScanPeriodMs < MinScanPeriodMs || point.ScanPeriodMs > MaxScanPeriodMs)
                issues.Add($"{label}：采集周期 {point.ScanPeriodMs}ms 超出范围（{MinScanPeriodMs}~{MaxScanPeriodMs}）");
            if (point.DataType == "String" && (point.LengthWords < 1 || point.LengthWords > MaxLengthWords))
                issues.Add($"{label}：字符串字数 {point.LengthWords} 超出范围（1~{MaxLengthWords}）");
        }

        // ---- 控件侧：量程与按钮写入值（与绑定点位类型对照） ----
        var pointsById = new Dictionary<string, PointDto>(StringComparer.Ordinal);
        foreach (var point in document.Points)
        {
            if (!string.IsNullOrWhiteSpace(point.Id))
                pointsById.TryAdd(point.Id, point);
        }

        foreach (var control in document.Screen.Controls)
        {
            var label = $"控件「{control.Title}」";
            if (control.Kind == ControlKind.LevelBar && control.MaxValue <= control.MinValue)
                issues.Add($"{label}：进度条量程上限（{control.MaxValue}）必须大于下限（{control.MinValue}）");
            if (control.Kind == ControlKind.PushButton
                && control.ActionMode == ButtonActionMode.WriteValue
                && control.BindingPointId is { } boundId
                && pointsById.TryGetValue(boundId, out var bound))
            {
                if (bound.DataType is "UInt16" or "Int32" or "Float32"
                    && !double.TryParse(control.WriteValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    issues.Add($"{label}：写入值「{control.WriteValue}」不是数值，与点位「{bound.Id}」的类型 {bound.DataType} 不符");
                else if (bound.DataType == "Bool"
                    && control.WriteValue.Trim().ToLowerInvariant() is not ("0" or "1" or "true" or "false" or "开" or "关"))
                    issues.Add($"{label}：写入值「{control.WriteValue}」不是合法的开关量（0/1/true/false/开/关）");
            }
        }

        return issues;
    }
}
