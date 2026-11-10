using HMISim.Common;
using HMISim.ProjectModel;

namespace HMISim.Controls.ViewModels;

/// <summary>
/// 播放中的一个控件项：包装工程里的 <see cref="ControlItem"/>（几何与绑定为静态直出），
/// 另持有运行时的显示值与质量（由播放器按节拍从实时数据库刷新）。
/// </summary>
public sealed class ControlItemViewModel : ObservableObject
{
    private string _displayValue = ValueFormatter.NoValuePlaceholder;
    private Quality _quality = Quality.NoData;

    /// <summary>包装一个控件项。</summary>
    public ControlItemViewModel(ControlItem item) => Item = item;

    /// <summary>工程中的控件项。</summary>
    public ControlItem Item { get; }

    /// <summary>画布 X（容器样式绑定用）。</summary>
    public double X => Item.X;

    /// <summary>画布 Y（容器样式绑定用）。</summary>
    public double Y => Item.Y;

    /// <summary>当前显示值（已格式化；Label 控件不用）。</summary>
    public string DisplayValue
    {
        get => _displayValue;
        private set => SetField(ref _displayValue, value);
    }

    /// <summary>当前质量码。</summary>
    public Quality Quality
    {
        get => _quality;
        private set => SetField(ref _quality, value);
    }

    /// <summary>用最新快照刷新（无绑定的控件不调用）。</summary>
    public void Apply(ValueSnapshot snapshot)
    {
        DisplayValue = ValueFormatter.Format(snapshot);
        Quality = snapshot.Quality;
    }
}
