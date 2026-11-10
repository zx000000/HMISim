namespace HMISim.Core;

/// <summary>
/// 需求注册表：调度模型的核心数据结构（原型书第 9 节）。
/// 画面、策略、报警、历史等消费者按「来源」登记对点位的采集需求；
/// 同一点位多方需求取最短周期（并集）；无人需求的点位不参与通讯。
/// 线程安全。
/// </summary>
public sealed class DemandRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Source, string PointId), TimeSpan> _demands = new();

    /// <summary>登记/更新一条需求（同一来源对同一点位重复登记即覆盖周期）。</summary>
    /// <param name="source">需求来源标识，如 "screen:主画面"、"history:1#存盘组"、"runtime"。</param>
    /// <param name="pointId">点位 Id。</param>
    /// <param name="period">期望采集周期。</param>
    public void Register(string source, string pointId, TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period), "采集周期必须为正");
        lock (_gate)
            _demands[(source, pointId)] = period;
    }

    /// <summary>注销某来源的全部需求（如画面关闭时注销该画面来源）。</summary>
    public void UnregisterSource(string source)
    {
        lock (_gate)
        {
            var keys = _demands.Keys.Where(k => k.Source == source).ToList();
            foreach (var key in keys)
                _demands.Remove(key);
        }
    }

    /// <summary>注销某来源对某一点位的需求。</summary>
    public void Unregister(string source, string pointId)
    {
        lock (_gate)
            _demands.Remove((source, pointId));
    }

    /// <summary>该点位当前的生效周期（全部需求中的最短值）；无人需求返回 null（不采集）。</summary>
    public TimeSpan? GetEffectivePeriod(string pointId)
    {
        lock (_gate)
        {
            TimeSpan? min = null;
            foreach (var ((_, p), period) in _demands)
            {
                if (p == pointId && (min is null || period < min))
                    min = period;
            }

            return min;
        }
    }

    /// <summary>该点位当前的需求方数量（引用计数）。</summary>
    public int GetDemandCount(string pointId)
    {
        lock (_gate)
            return _demands.Keys.Count(k => k.PointId == pointId);
    }
}
