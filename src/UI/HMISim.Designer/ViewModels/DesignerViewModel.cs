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

    /// <summary>缓冲：按钮动作模式名（枚举名）。</summary>
    public string EditActionMode { get; set; } = nameof(ButtonActionMode.Momentary);

    /// <summary>缓冲：按钮写入值。</summary>
    public string EditWriteValue { get; set; } = "1";

    /// <summary>缓冲：量程下限。</summary>
    public string EditMinValue { get; set; } = "0";

    /// <summary>缓冲：量程上限。</summary>
    public string EditMaxValue { get; set; } = "100";

    /// <summary>动作模式选项（枚举名列表，属性面板下拉用）。</summary>
    public IReadOnlyList<string> ActionModes { get; } = Enum.GetNames<ButtonActionMode>();

    /// <summary>是否有选中项（属性面板可用性）。</summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>选中项是否为按钮（属性面板显示动作字段）。</summary>
    public bool ShowButtonFields => SelectedItem?.Kind == ControlKind.PushButton;

    /// <summary>选中项是否用量程（进度条/数值输入，属性面板显示量程字段）。</summary>
    public bool ShowRangeFields => SelectedItem?.Kind is ControlKind.LevelBar or ControlKind.ValueInput;

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

    /// <summary>工具箱新增控件：按种类给缺省标题/尺寸、级联偏移放置，返回新控件。</summary>
    public ControlItem AddControl(ControlKind kind)
    {
        var offset = Document.Screen.Controls.Count % 5 * 24;
        var (title, width, height) = kind switch
        {
            ControlKind.ValueDisplay => ("新数值", 280d, 72d),
            ControlKind.PushButton => ("新按钮", 180d, 56d),
            ControlKind.IndicatorLamp => ("新指示灯", 220d, 64d),
            ControlKind.ValueInput => ("新输入", 300d, 104d),
            ControlKind.LevelBar => ("新进度条", 300d, 64d),
            _ => ("新标签", 200d, 40d),
        };
        var item = new ControlItem
        {
            Kind = kind,
            Title = title,
            BindingPointId = kind == ControlKind.Label ? null : PointIds.FirstOrDefault(),
            X = 40 + offset,
            Y = 40 + offset,
            Width = width,
            Height = height,
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
            EditActionMode = item.ActionMode.ToString();
            EditWriteValue = item.WriteValue;
            EditMinValue = item.MinValue.ToString("0.##", CultureInfo.InvariantCulture);
            EditMaxValue = item.MaxValue.ToString("0.##", CultureInfo.InvariantCulture);
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
        if (Enum.TryParse<ButtonActionMode>(EditActionMode, out var mode))
            item.ActionMode = mode;
        item.WriteValue = EditWriteValue;
        item.MinValue = ParseOr(EditMinValue, item.MinValue);
        item.MaxValue = ParseOr(EditMaxValue, item.MaxValue);
        StatusText = $"已应用属性到 {item.Title}";
        ItemEdited?.Invoke(item);
    }

    /// <summary>
    /// 应用点位/设备编辑结果（编辑窗口已校验通过）：替换设备与点位表，并处理画面绑定——
    /// 点位改名时绑定跟随新名，点位删除时指向它的绑定被清除（控件运行时显示无数据）。
    /// </summary>
    /// <param name="device">新设备定义。</param>
    /// <param name="points">新点位表。</param>
    /// <param name="useEmbeddedSimulator">模拟器开关。</param>
    /// <param name="rows">编辑行（OriginalId 用于识别改名与删除）。</param>
    public void ApplyDeviceAndPoints(DeviceDefinition device, List<PointDto> points, bool useEmbeddedSimulator, IReadOnlyList<PointEditRow> rows)
    {
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.OriginalId is { } original && original != row.Id.Trim())
                renames[original] = row.Id.Trim();
        }

        var survivingIds = new HashSet<string>(points.Select(p => p.Id), StringComparer.Ordinal);
        var renamed = 0;
        var cleared = 0;
        foreach (var control in Document.Screen.Controls)
        {
            if (control.BindingPointId is not { } bound)
                continue;
            if (renames.TryGetValue(bound, out var newId))
            {
                control.BindingPointId = newId;
                renamed++;
            }
            else if (!survivingIds.Contains(bound))
            {
                control.BindingPointId = null;
                cleared++;
            }
        }

        Document.Device = device;
        Document.Points = points;
        Document.UseEmbeddedSimulator = useEmbeddedSimulator;
        ReloadPointIds();
        StatusText = $"已更新点位表：共 {points.Count} 个点位"
            + (renamed > 0 ? $"，{renamed} 处绑定已跟随改名" : string.Empty)
            + (cleared > 0 ? $"，{cleared} 处失效绑定已清除" : string.Empty);
        DocumentReplaced?.Invoke();
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
        OnPropertyChanged(nameof(EditActionMode));
        OnPropertyChanged(nameof(EditWriteValue));
        OnPropertyChanged(nameof(EditMinValue));
        OnPropertyChanged(nameof(EditMaxValue));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowButtonFields));
        OnPropertyChanged(nameof(ShowRangeFields));
        OnPropertyChanged(nameof(SelectedItem));
    }

    private void OnPropertyChanged(string name) => RaisePropertyChanged(name);

    private static string Format(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static double ParseOr(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
