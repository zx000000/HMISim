using HMISim.Core;
using HMISim.DriverSdk;
using HMISim.Drivers.Fins;
using HMISim.ProjectModel;

namespace HMISim.Controls;

/// <summary>
/// 工程文档 → 内核运行时对象的映射（播放器专用）。
/// 本类是 ProjectModel（纯数据）与 Core（运行时）之间唯一的翻译点。
/// </summary>
public static class ProjectMapper
{
    /// <summary>按驱动 Id 创建驱动实例。阶段 1 仅内置 FINS；后续改为程序集扫描注册表。</summary>
    /// <exception cref="NotSupportedException">未知驱动 Id。</exception>
    public static IDeviceDriver CreateDriver(string driverId) => driverId switch
    {
        "fins" => new FinsDriver(),
        _ => throw new NotSupportedException($"未知驱动: {driverId}（阶段 1 仅内置 fins）"),
    };

    /// <summary>映射设备配置（参数字典字符串值直传，驱动侧按声明解析）。</summary>
    public static DeviceConfig MapDevice(ProjectDocument document) =>
        new(
            document.Device.DeviceId,
            document.Device.DriverId,
            document.Device.Parameters.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal));

    /// <summary>映射点位表。</summary>
    public static IReadOnlyList<PointDefinition> MapPoints(ProjectDocument document) =>
        document.Points
            .Select(p => new PointDefinition(
                p.Id,
                p.DeviceId,
                p.Address,
                Enum.Parse<TagDataType>(p.DataType),
                TimeSpan.FromMilliseconds(p.ScanPeriodMs),
                p.LengthWords))
            .ToList();
}
