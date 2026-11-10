using System.Globalization;
using HMISim.Controls.ViewModels;
using HMISim.ProjectModel;

namespace HMISim.Designer.ViewModels;

/// <summary>
/// 组态端主 ViewModel：持有当前工程文档与文件路径，管理选中项与属性编辑缓冲。
/// 文档变更通过事件通知窗口刷新画布（<see cref="DocumentReplaced"/> / <see cref="ItemEdited"/>），
/// 视图装配与文件对话框归窗口代码后置。
/// </summary>
public sealed class DesignerViewModel : ObservableObject
{
    private string _statusText = "就绪";

    /// <summary>创建 ViewModel（初始载入样例工程作为新建模板）。</summary>
    public DesignerViewModel()
    {
        Document = SampleProject.Create();
        ReloadPointIds();
    }

    /// <summary>文档被整体替换（新建/打开）时触发。</summary>
    public event Action? DocumentReplaced;

    /// <summary>某控件被属性面板编辑后触发（参数为该控件）。</summary>
    public event Action<ControlItem>? ItemEdited;

    /// <summary>当前工程文档。</summary>
    public ProjectDocument Document { get; private set; }

    /// <summary>当前文件路径（未保存过为 null）。</summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>当前选中控件（画布驱动）。</summary>
    public ControlItem? SelectedItem { get; private set; }

    /// <summary>工程内点位 Id 列表（绑定下拉用）。</summary>
    public IReadOnlyList<string> PointIds { get; private set; } = [];

    /// <summary>状态栏文本。</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    // ---- 属性编辑缓冲（选中时装载、点「应用」时写回） ----

    /// <summary>缓冲：标题。</summary>
    public string EditTitle { get; set; } = string.Empty;

    /// <summary>缓冲：绑定点位。</summary>
    public string? EditBindingPointId { get; set; }

    /// <summary>缓冲：X。</summary>
    public string EditX { get; set; } = "0";

    /// <summary>缓冲：Y。</summary>
    public string EditY { get; set; } = "0";

    /// <summary>缓冲：宽。</summary>
    public string EditWidth { get; set; } = "240";

    /// <summary>缓冲：高。</summary>
    public string EditHeight { get; set; } = "64";

    /// <summary>是否有选中项（属性面板可用性）。</summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>新建工程（以样例工程为模板）。</summary>
    public void NewDocument()
    {
        Document = SampleProject.Create();
        CurrentFilePath = null;
        SelectedItem = null;
        ReloadPointIds();
        StatusText = "已新建工程（样例模板）";
        DocumentReplaced?.Invoke();
    }

    /// <summary>从文件打开工程。</summary>
    public void LoadFrom(string path)
    {
        Document = ProjectFile.Load(path);
        CurrentFilePath = path;
        SelectedItem = null;
        ReloadPointIds();
        StatusText = $"已打开 {path}";
        DocumentReplaced?.Invoke();
    }

    /// <summary>保存到指定路径（同时记为当前路径）。</summary>
    public void SaveTo(string path)
    {
        ProjectFile.Save(path, Document);
        CurrentFilePath = path;
        StatusText = $"已保存 {path}";
    }

    /// <summary>工具箱新增控件：按种类生成、级联偏移放置，返回新控件。</summary>
    public ControlItem AddControl(ControlKind kind)
    {
        var offset = Document.Screen.Controls.Count % 5 * 24;
        var item = new ControlItem
        {
            Kind = kind,
            Title = kind == ControlKind.ValueDisplay ? "新数值" : "新标签",
            BindingPointId = kind == ControlKind.ValueDisplay ? PointIds.FirstOrDefault() : null,
            X = 40 + offset,
            Y = 40 + offset,
            Width = kind == ControlKind.ValueDisplay ? 280 : 200,
            Height = kind == ControlKind.ValueDisplay ? 72 : 40,
        };
        Document.Screen.Controls.Add(item);
        StatusText = $"已添加控件 {item.Title}";
        return item;
    }

    /// <summary>删除当前选中控件。</summary>
    public void DeleteSelected()
    {
        if (SelectedItem is null)
            return;
        Document.Screen.Controls.Remove(SelectedItem);
        StatusText = $"已删除控件 {SelectedItem.Title}";
        SelectedItem = null;
        DocumentReplaced?.Invoke();
    }

    /// <summary>画布选中变化时调用：装载属性缓冲。</summary>
    public void SelectItem(ControlItem? item)
    {
        SelectedItem = item;
        ReloadBuffer();
    }

    /// <summary>拖动/缩放结束后，把选中项的最新几何同步回属性缓冲。</summary>
    public void ReloadBuffer()
    {
        if (SelectedItem is { } item)
        {
            EditTitle = item.Title;
            EditBindingPointId = item.BindingPointId;
            EditX = Format(item.X);
            EditY = Format(item.Y);
            EditWidth = Format(item.Width);
            EditHeight = Format(item.Height);
        }

        RaiseBufferChanged();
    }

    /// <summary>把属性缓冲写回选中控件并通知画布刷新。</summary>
    public void ApplyEdits()
    {
        if (SelectedItem is not { } item)
            return;
        item.Title = EditTitle;
        item.BindingPointId = EditBindingPointId;
        item.X = ParseOr(EditX, item.X);
        item.Y = ParseOr(EditY, item.Y);
        item.Width = Math.Max(60, ParseOr(EditWidth, item.Width));
        item.Height = Math.Max(36, ParseOr(EditHeight, item.Height));
        StatusText = $"已应用属性到 {item.Title}";
        ItemEdited?.Invoke(item);
    }

    private void ReloadPointIds()
    {
        PointIds = Document.Points.Select(p => p.Id).ToList();
        OnPropertyChanged(nameof(PointIds));
    }

    private void RaiseBufferChanged()
    {
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(EditBindingPointId));
        OnPropertyChanged(nameof(EditX));
        OnPropertyChanged(nameof(EditY));
        OnPropertyChanged(nameof(EditWidth));
        OnPropertyChanged(nameof(EditHeight));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedItem));
    }

    private void OnPropertyChanged(string name) => RaisePropertyChanged(name);

    private static string Format(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static double ParseOr(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
