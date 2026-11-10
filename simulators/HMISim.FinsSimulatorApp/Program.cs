using HMISim.Simulators.Fins;

// 独立运行的 FINS 模拟器：dotnet run --project simulators/HMISim.FinsSimulatorApp -- [端口]
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 9600;
using var simulator = new FinsSimulator(port);
simulator.Start();
Console.WriteLine($"FINS 模拟器已启动，监听 UDP {port}。活数据：D0 计数器 / D2 温度(浮点) / D100.00 翻转。Ctrl+C 退出。");
var exit = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    exit.TrySetResult();
};
await exit.Task;
simulator.Stop();
