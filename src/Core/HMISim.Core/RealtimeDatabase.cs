using System.Collections.Concurrent;
using HMISim.Common;

namespace HMISim.Core;

/// <summary>
/// 实时数据库：全系统数据的唯一中枢。画面、策略、报警、历史只读它，驱动经调度器写它。
/// 线程安全；对外发布变化事件，订阅方在事件回调中不得反向调用写方法以免重入。
/// </summary>
public sealed class RealtimeDatabase
{
    private readonly ConcurrentDictionary<string, ValueSnapshot> _values = new(StringComparer.Ordinal);

    /// <summary>变量值变化事件（参数：点位 Id、新快照）。仅在值或质量实际变化时触发。</summary>
    public event Action<string, ValueSnapshot>? Changed;

    /// <summary>取点位当前快照；从未写入过返回 <see cref="ValueSnapshot.Empty"/>（质量 NoData）。</summary>
    public ValueSnapshot Get(string pointId) =>
        _values.TryGetValue(pointId, out var snapshot) ? snapshot : ValueSnapshot.Empty;

    /// <summary>写入新快照（由调度器调用）。</summary>
    public void Update(string pointId, object? value, Quality quality)
    {
        var snapshot = new ValueSnapshot(value, DateTimeOffset.Now, quality);
        var old = Get(pointId);
        _values[pointId] = snapshot;
        if (old.Value?.Equals(snapshot.Value) != true || old.Quality != snapshot.Quality)
            Changed?.Invoke(pointId, snapshot);
    }

    /// <summary>仅更新质量（保留旧值）：通讯失败降级时用，值冻结但质量变坏。</summary>
    public void UpdateQuality(string pointId, Quality quality)
    {
        var old = Get(pointId);
        if (old.Quality == quality)
            return;
        var snapshot = new ValueSnapshot(old.Value, old.Timestamp, quality);
        _values[pointId] = snapshot;
        Changed?.Invoke(pointId, snapshot);
    }
}
