using HMISim.Common;

namespace HMISim.Runtime.ViewModels;

/// <summary>
/// 一行点位显示的 ViewModel：只持有点位 Id、标题、格式化后的显示值与质量码，
/// 由主 ViewModel 按节拍从实时数据库快照刷新（合并节流，不逐变量推送）。
/// </summary>
public sealed class PointRowViewModel : ViewModelBase
{
    private string _displayValue = ValueFormatter.NoValuePlaceholder;
    private Quality _quality = Quality.NoData;

    /// <summary>创建一行。</summary>
    public PointRowViewModel(string pointId, string title)
    {
        PointId = pointId;
        Title = title;
    }

    /// <summary>点位 Id。</summary>
    public string PointId { get; }

    /// <summary>显示标题。</summary>
    public string Title { get; }

    /// <summary>当前显示值（已格式化）。</summary>
    public string DisplayValue
    {
        get => _displayValue;
        private set => SetField(ref _displayValue, value);
    }

    /// <summary>当前质量码（驱动控件变色）。</summary>
    public Quality Quality
    {
        get => _quality;
        private set => SetField(ref _quality, value);
    }

    /// <summary>用最新快照刷新本行。</summary>
    public void Apply(ValueSnapshot snapshot)
    {
        DisplayValue = ValueFormatter.Format(snapshot);
        Quality = snapshot.Quality;
    }
}
