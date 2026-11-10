using HMISim.Common;
using HMISim.Core;
using HMISim.DriverSdk;
using HMISim.Drivers.Fins;
using HMISim.Simulators.Fins;

// HMISim 阶段 0 测试（零外部依赖的控制台断言运行器）：dotnet run --project tests/HMISim.Tests
// 退出码 0 = 全过；非 0 = 有失败。

var total = 0;
var failures = 0;

void Check(bool condition, string name)
{
    total++;
    if (!condition)
    {
        failures++;
        Console.WriteLine($"  FAIL: {name}");
    }
}

void Eq<T>(T expected, T actual, string name) =>
    Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{name}（期望 {expected}，实际 {actual}）");

static string Hex(byte[] bytes) => Convert.ToHexString(bytes);
static ReadPoint Rp(string id, string address, TagDataType type, int lengthWords = 1) =>
    new(id, FinsAddress.Parse(address), type, lengthWords);

Console.WriteLine("[1] FinsCodec 金帧与编解码");
{
    var packet = FinsCodec.BuildPacket(0x2A, FinsCodec.BuildReadWordsCommand(100, 3));
    Eq("8000020000000000002A0101820064000003", Hex(packet), "读字帧字节");

    var writeBit = FinsCodec.BuildPacket(0x01, FinsCodec.BuildWriteBitCommand(FinsAddress.Parse("D100.05"), true));
    Eq("80000200000000000001010202006405000101", Hex(writeBit), "写位帧字节");

    var standard = FinsCodec.BuildPacket(0x00, new byte[] { 0x01, 0x01 })
        .Concat(new byte[] { 0x00, 0x00, 0x12, 0x34 }).ToArray();
    Eq("1234", Hex(FinsCodec.ParseResponseData(standard)), "标准响应解析");

    // HSL 风格：位读（数据紧跟 MRES@12 之后，SRES 缺省）；下标 10/11 为其多出的 2 字节回显（非零，否则会命中标准分支）
    var hslBit = new byte[14];
    hslBit[10] = 0x01;
    hslBit[11] = 0x01;
    hslBit[12] = 0x00;
    hslBit[13] = 0x01;
    Eq("01", Hex(FinsCodec.ParseResponseData(hslBit)), "HSL 位读响应解析");
    // HSL 风格：字读（MRES@12、SRES@13）
    var hslWord = new byte[16];
    hslWord[10] = 0x01;
    hslWord[11] = 0x01;
    hslWord[14] = 0xAB;
    hslWord[15] = 0xCD;
    Eq("ABCD", Hex(FinsCodec.ParseResponseData(hslWord)), "HSL 字读响应解析");

    var threw = false;
    try
    {
        FinsCodec.ParseResponseData(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x01, 0x05, 0x00 });
    }
    catch (InvalidOperationException)
    {
        threw = true;
    }

    Check(threw, "错误结束码抛异常");

    Eq(-123456, (int)FinsCodec.DecodeValue(FinsCodec.EncodeValue(-123456, TagDataType.Int32, 1), TagDataType.Int32), "Int32 编解码往返");
    var floatBack = (float)FinsCodec.DecodeValue(FinsCodec.EncodeValue(36.5f, TagDataType.Float32, 1), TagDataType.Float32);
    Check(Math.Abs(floatBack - 36.5f) < 0.001f, "Float32 编解码往返");
    Eq("SN1234", (string)FinsCodec.DecodeValue(FinsCodec.EncodeValue("SN1234", TagDataType.String, 3), TagDataType.String), "String 编解码往返");

    var addr = FinsAddress.Parse("D100.05");
    Eq((ushort)100, addr.Word, "地址解析字");
    Eq((byte)5, addr.Bit!.Value, "地址解析位");
    Check(FinsAddress.Parse("d200").Bit is null, "小写字地址解析");
}

Console.WriteLine("[2] FinsBlockPlanner 合块");
{
    var blocks = FinsBlockPlanner.Plan(new[]
    {
        Rp("a", "D0", TagDataType.UInt16),
        Rp("b", "D1", TagDataType.UInt16),
        Rp("c", "D3", TagDataType.UInt16),
        Rp("d", "D10", TagDataType.UInt16),
        Rp("e", "D500", TagDataType.UInt16),
        Rp("f", "D100.00", TagDataType.Bool),
    });
    Eq(4, blocks.Count, "块总数（3 字块 + 1 位块）");
    var first = (WordBlock)blocks.First(b => b is WordBlock);
    Eq((ushort)0, first.StartWord, "首块起始");
    Eq(4, first.WordCount, "首块字数（D0,D1 + 间隙1 + D3）");
    Eq(3, first.Items.Count, "首块点位数");

    var split = FinsBlockPlanner.Plan(
        new[] { Rp("a", "D0", TagDataType.UInt16), Rp("b", "D1", TagDataType.UInt16), Rp("c", "D2", TagDataType.UInt16) },
        maxWordsPerFrame: 2);
    Eq(2, split.Count, "单帧上限切分");

    var noGap = FinsBlockPlanner.Plan(
        new[] { Rp("a", "D0", TagDataType.UInt16), Rp("b", "D2", TagDataType.UInt16) },
        maxGapWords: 0);
    Eq(2, noGap.Count, "间隙 0 时不合并");
}

Console.WriteLine("[3] DemandRegistry 需求并集");
{
    var registry = new DemandRegistry();
    var p = "P1";
    Check(registry.GetEffectivePeriod(p) is null, "无需求不采集");
    registry.Register("runtime", p, TimeSpan.FromMilliseconds(1000));
    registry.Register("screen:主画面", p, TimeSpan.FromMilliseconds(100));
    Eq(100.0, registry.GetEffectivePeriod(p)!.Value.TotalMilliseconds, "多方需求取最短");
    Eq(2, registry.GetDemandCount(p), "引用计数");
    registry.UnregisterSource("screen:主画面");
    Eq(1000.0, registry.GetEffectivePeriod(p)!.Value.TotalMilliseconds, "画面关闭后回落");
    registry.UnregisterSource("runtime");
    Check(registry.GetEffectivePeriod(p) is null, "需求归零退出采集");
}

Console.WriteLine("[4] 模拟器帧处理");
{
    using var simulator = new FinsSimulator(19600);
    simulator.SetWord(0, 0x1234);
    simulator.SetWord(1, 0xABCD);
    var request = FinsCodec.BuildPacket(0x07, FinsCodec.BuildReadWordsCommand(0, 2));
    var response = simulator.HandleRequest(request);
    Check(response is not null, "模拟器有响应");
    Eq("1234ABCD", Hex(FinsCodec.ParseResponseData(response!)), "模拟器读字数据");

    var writeRequest = FinsCodec.BuildPacket(0x08, FinsCodec.BuildWriteWordsCommand(200, new ushort[] { 0x7788 }));
    simulator.HandleRequest(writeRequest);
    Eq((ushort)0x7788, simulator.GetWord(200), "模拟器写字落地");
}

Console.WriteLine("[5] DeviceRuntime 调度（假会话）");
{
    // A：有需求 → 周期采集且质量 Good、值递增
    var sessionA = new FakeSession();
    var runtimeA = MakeRuntime(sessionA, out var dbA, out var registryA);
    registryA.Register("runtime", "P1", TimeSpan.FromMilliseconds(100));
    await RunFor(runtimeA, 450);
    Eq(Quality.Good, dbA.Get("P1").Quality, "A 质量 Good");
    Check(sessionA.ReadCalls >= 3, $"A 读取次数 >=3（实际 {sessionA.ReadCalls}）");
    Check(dbA.Get("P1").Value is int v && v >= 2, "A 值在递增");

    // B：无需求 → 零通讯、质量停在 NoData
    var sessionB = new FakeSession();
    var runtimeB = MakeRuntime(sessionB, out var dbB, out _);
    await RunFor(runtimeB, 300);
    Eq(0, sessionB.ReadCalls, "B 无需求零读取");
    Eq(Quality.NoData, dbB.Get("P1").Quality, "B 质量 NoData");

    // C：写优先——启动前入队的写必须先于第一次读执行
    var sessionC = new FakeSession();
    var runtimeC = MakeRuntime(sessionC, out var dbC, out var registryC);
    registryC.Register("runtime", "P1", TimeSpan.FromMilliseconds(100));
    runtimeC.TryEnqueueWrite("P1", 77);
    await RunFor(runtimeC, 250);
    Check(sessionC.OpLog.Count > 0 && sessionC.OpLog[0] == "W", "C 写先于读");
    Eq(77, sessionC.LastWritten, "C 写入值到达会话");

    // D：失败降级 Uncertain → Bad，恢复后回 Good
    var sessionD = new FakeSession { FailMode = true };
    var runtimeD = MakeRuntime(sessionD, out var dbD, out var registryD);
    registryD.Register("runtime", "P1", TimeSpan.FromMilliseconds(100));
    using var ctsD = new CancellationTokenSource();
    var loopD = Task.Run(() => runtimeD.RunAsync(ctsD.Token));
    await Task.Delay(750);
    Eq(Quality.Bad, dbD.Get("P1").Quality, "D 连续失败判 Bad");
    sessionD.FailMode = false;
    await Task.Delay(450);
    ctsD.Cancel();
    await loopD;
    Eq(Quality.Good, dbD.Get("P1").Quality, "D 恢复后回 Good");
}

Console.WriteLine($"== 测试结束：{total - failures}/{total} 通过 ==");
return failures == 0 ? 0 : 1;

static DeviceRuntime MakeRuntime(FakeSession session, out RealtimeDatabase db, out DemandRegistry registry)
{
    db = new RealtimeDatabase();
    registry = new DemandRegistry();
    var points = new List<PointDefinition>
    {
        new("P1", "DEV1", "D0", TagDataType.UInt16, TimeSpan.FromMilliseconds(100)),
    };
    return new DeviceRuntime("DEV1", points, session, db, registry);
}

static async Task RunFor(DeviceRuntime runtime, int milliseconds)
{
    using var cts = new CancellationTokenSource();
    var loop = Task.Run(() => runtime.RunAsync(cts.Token));
    await Task.Delay(milliseconds);
    cts.Cancel();
    await loop;
}

internal sealed record TestAddress(string Raw) : IPointAddress;

internal sealed class FakeSession : IDeviceSession
{
    public bool FailMode { get; set; }
    public int ReadCalls { get; private set; }
    public List<string> OpLog { get; } = [];
    public int LastWritten { get; private set; } = -1;
    private readonly Dictionary<string, int> _counters = new();

    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public IPointAddress ParseAddress(string raw) => new TestAddress(raw);

    public Task<IReadOnlyList<PointValue>> ReadAsync(IReadOnlyList<ReadPoint> points, CancellationToken cancellationToken)
    {
        ReadCalls++;
        OpLog.Add("R");
        IReadOnlyList<PointValue> results = points.Select(p =>
        {
            if (FailMode)
                return PointValue.Fail(p.PointId, "假会话故障注入");
            var next = _counters.GetValueOrDefault(p.PointId) + 1;
            _counters[p.PointId] = next;
            return PointValue.Ok(p.PointId, next);
        }).ToList();
        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<PointValue>> WriteAsync(IReadOnlyList<WritePoint> points, CancellationToken cancellationToken)
    {
        OpLog.Add("W");
        foreach (var point in points)
            LastWritten = Convert.ToInt32(point.Value);
        IReadOnlyList<PointValue> results = points.Select(p => PointValue.Ok(p.PointId, p.Value)).ToList();
        return Task.FromResult(results);
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
