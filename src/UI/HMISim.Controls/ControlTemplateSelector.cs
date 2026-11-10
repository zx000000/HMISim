using System.Windows;
using System.Windows.Controls;
using HMISim.Controls.ViewModels;
using HMISim.ProjectModel;

namespace HMISim.Controls;

/// <summary>按控件种类选择模板（ValueDisplay / Label）。</summary>
public sealed class ControlTemplateSelector : DataTemplateSelector
{
    /// <summary>数值显示模板。</summary>
    public DataTemplate? ValueDisplayTemplate { get; set; }

    /// <summary>文本标签模板。</summary>
    public DataTemplate? LabelTemplate { get; set; }

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is ControlItemViewModel viewModel
            ? viewModel.Item.Kind == ControlKind.ValueDisplay ? ValueDisplayTemplate : LabelTemplate
            : base.SelectTemplate(item, container);
}
