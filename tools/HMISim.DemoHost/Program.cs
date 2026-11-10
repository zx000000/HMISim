using HMISim.Common;
using HMISim.Core;
using HMISim.DriverSdk;
using HMISim.Drivers.Fins;
using HMISim.Simulators.Fins;

// HMISim 阶段 0 全链路演示：进程内 FINS 模拟器 → FINS 驱动 → 调度器 → 实时数据库。
// 演示内容：周期采集、多需求取最短周期、写优先回填、"拔线"后质量 Uncertain → Bad。

var logger = new ConsoleLogger();

// 1) 启动模拟器（相当于一台欧姆龙 PLC）
using var simulator = new FinsSimulator(9600);
simulator.Start();
Console.WriteLine("== 模拟器已启动 (UDP 9600) ==");

// 2) 装配运行时：一台 FINS 设备 + 4 个点位
var host = new RuntimeHost(logger);
var driver = new FinsDriver();
var config = new DeviceConfig("PLC1", driver.DriverId, new Dictionary<string, object?>
{
    ["Host"] = "127.0.0.1",
    ["Port"] = 9600,
    ["LocalPort"] = 9601,
    ["TimeoutMs"] = 300,
});
var points = new List<PointDefinition>
{
    new("计数器", "PLC1", "D0", TagDataType.UInt16, TimeSpan.FromMilliseconds(200)),
    new("温度", "PLC1", "D2", TagDataType.Float32, TimeSpan.FromMilliseconds(500)),
    new("运行信号", "PLC1", "D100.00", TagDataType.Bool, TimeSpan.FromMilliseconds(200)),
    new("设定值", "PLC1", "D200", TagDataType.UInt16, TimeSpan.FromMilliseconds(1000)),
};
var device = host.AddDevice(config, driver, points);

// "演示画面"再给温度叠加 100ms 的加速需求：生效周期应为 min(500, 100) = 100ms
host.Registry.Register("screen:演示画面", "温度", TimeSpan.FromMilliseconds(100));
Console.WriteLine($"温度生效周期 = {host.Registry.GetEffectivePeriod("温度")?.TotalMilliseconds}ms（期望 100）");

host.Start();
Console.WriteLine("== 运行时已启动，采样 6 秒 ==");
for (var i = 0; i < 6; i++)
{
    await Task.Delay(1000);
    PrintTable(host);
}

// 3) 写演示：设定值 1234 经写队列下发，并应立即回填数据库
Console.WriteLine("== 写入 设定值 = 1234 ==");
device.TryEnqueueWrite("设定值", (ushort)1234);
await Task.Delay(800);
PrintTable(host);
Console.WriteLine($"模拟器内存 D200 = {simulator.GetWord(200)}（期望 1234）");

// 4) 拔线演示：停掉模拟器，质量应从 Good → Uncertain → Bad，值冻结
Console.WriteLine("== 模拟拔线（停止模拟器）==");
simulator.Stop();
for (var i = 0; i < 4; i++)
{
    await Task.Delay(1000);
    PrintTable(host);
}

await host.StopAsync();
Console.WriteLine($"== 演示结束：成功帧 {device.SuccessfulFrames}，失败帧 {device.FailedFrames}，最近成功 {device.LastSuccessAt:HH:mm:ss} ==");
return;

void PrintTable(RuntimeHost runtimeHost)
{
    Console.WriteLine($"-- {DateTime.Now:HH:mm:ss} --");
    foreach (var point in points)
    {
        var snapshot = runtimeHost.Database.Get(point.PointId);
        var value = snapshot.Value switch
        {
            float f => f.ToString("F2"),
            _ => snapshot.Value?.ToString() ?? "(无)",
        };
        Console.WriteLine($"  {point.PointId,-6} {value,-10} 质量={snapshot.Quality}");
    }
}

internal sealed class ConsoleLogger : IAppLogger
{
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");

    public void Warn(string message) => Console.WriteLine($"[WARN] {message}");

    public void Error(string message, Exception? exception = null) =>
        Console.WriteLine($"[ERROR] {message}{(exception is null ? string.Empty : $" ({exception.GetType().Name}: {exception.Message})")}");
}
