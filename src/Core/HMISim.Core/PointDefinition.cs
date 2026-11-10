using HMISim.DriverSdk;

namespace HMISim.Core;

/// <summary>
/// 点位（变量）定义：变量层只认「设备 + 定位串 + 数据类型」，不感知协议细节。
/// </summary>
/// <param name="PointId">变量唯一标识。</param>
/// <param name="DeviceId">所属设备。</param>
/// <param name="Address">定位串（地址或符号路径），由所属驱动解析。</param>
/// <param name="DataType">数据类型。</param>
/// <param name="ScanPeriod">缺省采集周期（无任何需求登记时的兜底不生效，见需求注册表）。</param>
/// <param name="LengthWords">字符串类型的字数；其余类型忽略。</param>
public sealed record PointDefinition(
    string PointId,
    string DeviceId,
    string Address,
    TagDataType DataType,
    TimeSpan ScanPeriod,
    int LengthWords = 1);
