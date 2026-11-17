using System.Windows;
using System.Windows.Controls;
using HMISim.Controls.ViewModels;
using HMISim.ProjectModel;

namespace HMISim.Controls;

/// <summary>按控件种类选择模板（六种常用控件）。</summary>
public sealed class ControlTemplateSelector : DataTemplateSelector
{
    /// <summary>数值显示模板。</summary>
    public DataTemplate? ValueDisplayTemplate { get; set; }

    /// <summary>文本标签模板。</summary>
    public DataTemplate? LabelTemplate { get; set; }

    /// <summary>按钮模板。</summary>
    public DataTemplate? PushButtonTemplate { get; set; }

    /// <summary>指示灯模板。</summary>
    public DataTemplate? IndicatorLampTemplate { get; set; }

    /// <summary>数值输入模板。</summary>
    public DataTemplate? ValueInputTemplate { get; set; }

    /// <summary>进度条模板。</summary>
    public DataTemplate? LevelBarTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is ControlItemViewModel viewModel
            ? viewModel.Item.Kind switch
            {
                ControlKind.ValueDisplay => ValueDisplayTemplate,
                ControlKind.PushButton => PushButtonTemplate,
                ControlKind.IndicatorLamp => IndicatorLampTemplate,
                ControlKind.ValueInput => ValueInputTemplate,
                ControlKind.LevelBar => LevelBarTemplate,
                _ => LabelTemplate,
            }
            : base.SelectTemplate(item, container);
}
