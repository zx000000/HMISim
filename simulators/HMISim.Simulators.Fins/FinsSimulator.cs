using System.Net;
using System.Net.Sockets;

namespace HMISim.Simulators.Fins;

/// <summary>
/// FINS/UDP PLC 模拟器（阶段 0 联调用）：内存中维护 D 区，响应 0101 读 / 0102 写，
/// 并周期性生成活数据——D0 计数器、D2 起始的浮点温度（低字在前）、D100.00 运行信号翻转。
/// 模式沿用 SnCollector 的 SnSimulator。
/// </summary>
public sealed class FinsSimulator : IDisposable
{
    private readonly object _gate = new();
    private readonly ushort[] _dm = new ushort[32768];
    private UdpClient? _udp;
    private CancellationTokenSource? _stopSource;
    private Task? _serveLoop;
    private Task? _generatorLoop;

    /// <summary>监听端口。</summary>
    public int Port { get; }

    /// <summary>创建模拟器。</summary>
    public FinsSimulator(int port = 9600) => Port = port;

    /// <summary>直接读内存字（测试/演示用）。</summary>
    public ushort GetWord(int address)
    {
        lock (_gate)
            return _dm[address];
    }

    /// <summary>直接写内存字（测试/演示用）。</summary>
    public void SetWord(int address, ushort value)
    {
        lock (_gate)
            _dm[address] = value;
    }

    /// <summary>启动服务与数据生成。</summary>
    public void Start()
    {
        _stopSource = new CancellationTokenSource();
        _udp = new UdpClient(Port);
        if (OperatingSystem.IsWindows())
            _udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0, 0, 0, 0 }, null);
        _serveLoop = Task.Run(() => ServeAsync(_stopSource.Token));
        _generatorLoop = Task.Run(() => GenerateAsync(_stopSource.Token));
    }

    /// <summary>停止服务（演示中用于模拟"拔线"）。</summary>
    public void Stop()
    {
        _stopSource?.Cancel();
        _udp?.Dispose();
        _udp = null;
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _udp is not null)
        {
            UdpReceiveResult received;
            try
            {
                received = await _udp.ReceiveAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                break;
            }

            var response = HandleRequest(received.Buffer);
            if (response is not null)
            {
                try
                {
                    await _udp.SendAsync(response, received.RemoteEndPoint, cancellationToken);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or SocketException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>处理一帧请求并生成响应（标准 FINS 布局）。无法识别的命令返回 null（不响应）。公开以便金帧测试直接调用。</summary>
    public byte[]? HandleRequest(byte[] request)
    {
        if (request.Length < 18)
            return null;
        var mrc = request[10];
        var src = request[11];
        var area = request[12];
        var address = (request[13] << 8) | request[14];
        var bit = request[15];

        var response = new List<byte>(request.Take(10));
        if (mrc == 0x01 && src == 0x01) // 读
        {
            response.AddRange([0x00, 0x00]);
            if (area == 0x82)
            {
                var count = (request[16] << 8) | request[17];
                lock (_gate)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var word = address + i < _dm.Length ? _dm[address + i] : (ushort)0;
                        response.Add((byte)(word >> 8));
                        response.Add((byte)(word & 0xFF));
                    }
                }
            }
            else if (area == 0x02)
            {
                lock (_gate)
                    response.Add((byte)((_dm[address] >> bit) & 1));
            }
            else
            {
                return null;
            }
        }
        else if (mrc == 0x01 && src == 0x02) // 写
        {
            if (area == 0x02)
            {
                lock (_gate)
                {
                    if (request[17] != 0)
                        _dm[address] = (ushort)(_dm[address] | (1 << bit));
                    else
                        _dm[address] = (ushort)(_dm[address] & ~(1 << bit));
                }
            }
            else if (area == 0x82)
            {
                var count = (request[16] << 8) | request[17];
                lock (_gate)
                {
                    for (var i = 0; i < count && 18 + i * 2 + 1 < request.Length; i++)
                        _dm[address + i] = (ushort)((request[18 + i * 2] << 8) | request[18 + i * 2 + 1]);
                }
            }
            else
            {
                return null;
            }

            response.AddRange([0x00, 0x00]);
        }
        else
        {
            return null;
        }

        return response.ToArray();
    }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        var tick = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            tick++;
            lock (_gate)
            {
                _dm[0] = (ushort)(_dm[0] + 1); // D0 计数器
                var temperature = 25.0f + 5.0f * MathF.Sin(tick / 6.0f);
                var bits = BitConverter.SingleToUInt32Bits(temperature);
                _dm[2] = (ushort)(bits & 0xFFFF); // D2/D3 温度（低字在前）
                _dm[3] = (ushort)((bits >> 16) & 0xFFFF);
                if (tick % 4 == 0)
                    _dm[100] = (ushort)(_dm[100] ^ 1); // D100.00 每 2 秒翻转
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
