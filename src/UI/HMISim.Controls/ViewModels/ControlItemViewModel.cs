using HMISim.Common;
using HMISim.Core;
using HMISim.DriverSdk;
using HMISim.ProjectModel;

namespace HMISim.Controls.ViewModels;

/// <summary>
/// 播放中的一个控件项：包装工程里的 <see cref="ControlItem"/>（几何与绑定为静态直出），
/// 另持有运行时的显示值与质量（由播放器按节拍从实时数据库刷新）。
/// 交互控件（按钮/数值输入）的写入经构造时注入的 writeSink 出去——sink 由播放器
/// 接到设备运行时的优先写队列，本类不直接依赖运行时。
/// </summary>
public sealed class ControlItemViewModel : ObservableObject
{
    private readonly TagDataType? _boundType;
    private readonly Action<string, object>? _writeSink;
    private string _displayValue = ValueFormatter.NoValuePlaceholder;
    private Quality _quality = Quality.NoData;
    private bool _isOn;
    private double _barFraction;
    private string? _inputError;

    /// <summary>包装一个控件项（boundType/writeSink 供交互控件使用，纯显示控件可省略）。</summary>
    public ControlItemViewModel(ControlItem item, TagDataType? boundType = null, Action<string, object>? writeSink = null)
    {
        Item = item;
        _boundType = boundType;
        _writeSink = writeSink;
    }

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

    /// <summary>最近一次快照的原始值（切换按钮取反、指示灯判定用）。</summary>
    public object? RawValue { get; private set; }

    /// <summary>指示灯状态：Bool 直接取真值，数值非 0 视为真，无数据为假。</summary>
    public bool IsOn
    {
        get => _isOn;
        private set => SetField(ref _isOn, value);
    }

    /// <summary>进度条填充比例（0~1，按控件量程 MinValue~MaxValue 换算并夹紧）。</summary>
    public double BarFraction
    {
        get => _barFraction;
        private set => SetField(ref _barFraction, value);
    }

    /// <summary>数值输入的最近一次错误（转换/量程），成功写入后清空。</summary>
    public string? InputError
    {
        get => _inputError;
        private set => SetField(ref _inputError, value);
    }

    /// <summary>用最新快照刷新（无绑定的控件不调用）。</summary>
    public void Apply(ValueSnapshot snapshot)
    {
        DisplayValue = ValueFormatter.Format(snapshot);
        Quality = snapshot.Quality;
        RawValue = snapshot.Value;
        IsOn = snapshot.Value switch
        {
            bool b => b,
            null => false,
            _ => Convert.ToDouble(snapshot.Value, System.Globalization.CultureInfo.InvariantCulture) != 0,
        };
        BarFraction = snapshot.Value is not null && Item.MaxValue > Item.MinValue
            ? Math.Clamp(
                (Convert.ToDouble(snapshot.Value, System.Globalization.CultureInfo.InvariantCulture) - Item.MinValue)
                / (Item.MaxValue - Item.MinValue), 0, 1)
            : 0;
    }

    /// <summary>按钮按下/点击（按动作模式决定写入内容；点动模式的复位在 <see cref="ReleaseButton"/>）。</summary>
    public void PressButton()
    {
        if (Item.BindingPointId is not { } pointId || _writeSink is null || _boundType is not { } type)
            return;
        switch (Item.ActionMode)
        {
            case ButtonActionMode.Momentary:
            case ButtonActionMode.Set:
            case ButtonActionMode.WriteValue:
                _writeSink(pointId, OnValue(type));
                break;
            case ButtonActionMode.Reset:
                _writeSink(pointId, OffValue(type));
                break;
            case ButtonActionMode.Toggle:
                if (type == TagDataType.Bool)
                    _writeSink(pointId, RawValue is not bool current || !current);
                break;
        }
    }

    /// <summary>按钮松开（仅点动模式：写「关」值）。</summary>
    public void ReleaseButton()
    {
        if (Item.ActionMode != ButtonActionMode.Momentary)
            return;
        if (Item.BindingPointId is { } pointId && _writeSink is not null && _boundType is { } type)
            _writeSink(pointId, OffValue(type));
    }

    /// <summary>数值输入确认：按点位类型转换并做量程校验（量程在 Max 大于 Min 时生效），成功才写入。</summary>
    public void SubmitInput(string text)
    {
        if (Item.BindingPointId is not { } pointId || _writeSink is null || _boundType is not { } type)
            return;
        if (!PointValueParser.TryParse(text, type, out var value, out var error))
        {
            InputError = error;
            return;
        }

        if (value is not string && Item.MaxValue > Item.MinValue)
        {
            var numeric = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            if (numeric < Item.MinValue || numeric > Item.MaxValue)
            {
                InputError = $"超出量程 {Item.MinValue}~{Item.MaxValue}";
                return;
            }
        }

        InputError = null;
        _writeSink(pointId, value!);
    }

    /// <summary>「开」值：WriteValue 按点位类型转换；转换失败（组态期应已拦截）时回退到类型缺省开值。</summary>
    private object OnValue(TagDataType type)
    {
        if (PointValueParser.TryParse(Item.WriteValue, type, out var parsed, out _))
            return parsed!;
        return type switch
        {
            TagDataType.Bool => true,
            TagDataType.UInt16 => (ushort)1,
            TagDataType.Int32 => 1,
            TagDataType.Float32 => 1f,
            _ => Item.WriteValue,
        };
    }

    private static object OffValue(TagDataType type) => type switch
    {
        TagDataType.Bool => false,
        TagDataType.UInt16 => (ushort)0,
        TagDataType.Int32 => 0,
        TagDataType.Float32 => 0f,
        _ => string.Empty,
    };
}
