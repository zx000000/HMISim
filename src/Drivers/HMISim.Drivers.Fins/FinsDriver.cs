using HMISim.DriverSdk;

namespace HMISim.Drivers.Fins;

/// <summary>
/// 欧姆龙 FINS (UDP) 驱动。阶段 0 支持 D 区字/位读写；
/// 代码基础移植自 SnCollector 的 FinsUdpClient（putao 本人项目），本项目内重新组织为驱动契约实现。
/// </summary>
public sealed class FinsDriver : IDeviceDriver
{
    /// <inheritdoc />
    public string DriverId => "fins";

    /// <inheritdoc />
    public string DisplayName => "欧姆龙 FINS (UDP)";

    /// <inheritdoc />
    public IReadOnlyList<DriverParameterDef> Parameters { get; } =
    [
        new("Host", "PLC IP 地址", DriverParamKind.String, "192.168.1.250", true),
        new("Port", "PLC 端口", DriverParamKind.Int, 9600, false),
        new("LocalPort", "本机端口", DriverParamKind.Int, 9601, false),
        new("TimeoutMs", "通讯超时 (ms)", DriverParamKind.Int, 1000, false),
    ];

    /// <inheritdoc />
    public IDeviceSession CreateSession(DeviceConfig config) => new FinsUdpSession(config);
}
