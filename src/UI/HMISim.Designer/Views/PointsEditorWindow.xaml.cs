using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HMISim.Controls;
using HMISim.Designer.ViewModels;
using HMISim.DriverSdk;
using HMISim.ProjectModel;

namespace HMISim.Designer.Views;

/// <summary>
/// 点位 / 设备编辑窗口（模态）：在文档副本上编辑，点「确定」时统一校验——
/// 结构校验走 <see cref="ProjectValidator"/>，参数整数性与地址格式走驱动声明与
/// 驱动会话的 ParseAddress。校验不过不关窗，问题清单显示在窗口底部。
/// </summary>
public partial class PointsEditorWindow : Window
{
    /// <summary>可选驱动（阶段 1 仅内置 FINS；后续改为驱动注册表填充）。</summary>
    private sealed record DriverChoice(string DriverId, string DisplayName);

    private static readonly DriverChoice[] KnownDrivers = [new("fins", "欧姆龙 FINS (UDP)")];

    private readonly ProjectDocument _source;
    private readonly ObservableCollection<PointEditRow> _rows = [];
    private readonly List<(DriverParameterDef Def, TextBox Box)> _paramEditors = [];

    /// <summary>以当前工程文档初始化编辑副本。</summary>
    public PointsEditorWindow(ProjectDocument document)
    {
        InitializeComponent();
        _source = document;

        DeviceIdBox.Text = document.Device.DeviceId;
        DriverBox.ItemsSource = KnownDrivers;
        DriverBox.DisplayMemberPath = nameof(DriverChoice.DisplayName);
        DriverBox.SelectedItem = KnownDrivers.FirstOrDefault(d => d.DriverId == document.Device.DriverId) ?? KnownDrivers[0];
        SimulatorCheck.IsChecked = document.UseEmbeddedSimulator;
        BuildParamRows(document.Device.DriverId, document.Device.Parameters);

        foreach (var point in document.Points)
        {
            _rows.Add(new PointEditRow
            {
                OriginalId = point.Id,
                Id = point.Id,
                Address = point.Address,
                DataType = point.DataType,
                ScanPeriodMs = point.ScanPeriodMs.ToString(CultureInfo.InvariantCulture),
                LengthWords = point.LengthWords.ToString(CultureInfo.InvariantCulture),
            });
        }

        PointsGrid.ItemsSource = _rows;
        ((DataGridComboBoxColumn)PointsGrid.Columns[2]).ItemsSource = ProjectValidator.KnownDataTypes;
    }

    /// <summary>校验通过后的设备定义（点「确定」后有效）。</summary>
    public DeviceDefinition EditedDevice { get; private set; } = new();

    /// <summary>校验通过后的点位表（点「确定」后有效）。</summary>
    public List<PointDto> EditedPoints { get; private set; } = [];

    /// <summary>校验通过后的模拟器开关（点「确定」后有效）。</summary>
    public bool EditedUseEmbeddedSimulator { get; private set; }

    /// <summary>编辑行（含 OriginalId，供主 ViewModel 计算改名跟随与删除清理）。</summary>
    public IReadOnlyList<PointEditRow> Rows => _rows;

    private void BuildParamRows(string driverId, IReadOnlyDictionary<string, string> values)
    {
        ParamRows.Children.Clear();
        _paramEditors.Clear();
        IDeviceDriver driver;
        try
        {
            driver = ProjectMapper.CreateDriver(driverId);
        }
        catch (NotSupportedException)
        {
            return;
        }

        foreach (var def in driver.Parameters)
        {
            var box = new TextBox
            {
                Text = values.TryGetValue(def.Key, out var existing)
                    ? existing
                    : def.DefaultValue?.ToString() ?? string.Empty,
                Margin = new Thickness(0, 3, 0, 3),
                Padding = new Thickness(4, 2, 4, 2),
            };
            var row = new DockPanel { LastChildFill = true };
            var label = new TextBlock
            {
                Text = $"{def.DisplayName}（{def.Key}）",
                Width = 190,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(box);
            ParamRows.Children.Add(row);
            _paramEditors.Add((def, box));
        }
    }

    private Dictionary<string, string> CollectParamValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (def, box) in _paramEditors)
            values[def.Key] = box.Text.Trim();
        return values;
    }

    private void DriverBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || DriverBox.SelectedItem is not DriverChoice choice)
            return;
        // 切换驱动时保留已填的同名参数，其余按新驱动声明与默认值重建
        var carried = CollectParamValues();
        foreach (var (key, value) in _source.Device.Parameters)
            carried.TryAdd(key, value);
        BuildParamRows(choice.DriverId, carried);
    }

    private void AddPointButton_OnClick(object sender, RoutedEventArgs e)
    {
        var baseName = "新点位";
        var name = baseName;
        var suffix = 2;
        while (_rows.Any(r => r.Id == name))
            name = $"{baseName}{suffix++}";
        _rows.Add(new PointEditRow { Id = name, Address = "D0", DataType = "UInt16", ScanPeriodMs = "500", LengthWords = "1" });
    }

    private void DeletePointButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (PointsGrid.SelectedItem is PointEditRow row)
            _rows.Remove(row);
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private async void OkButton_OnClick(object sender, RoutedEventArgs e)
    {
        PointsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PointsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var issues = new List<string>();
        var deviceId = DeviceIdBox.Text.Trim();
        var driverId = (DriverBox.SelectedItem as DriverChoice)?.DriverId ?? string.Empty;

        // ---- 设备参数：按驱动声明校验整数/必填，其余原样保留（含未知键，向前兼容） ----
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in _source.Device.Parameters)
            parameters[key] = value;
        foreach (var (def, box) in _paramEditors)
        {
            var text = box.Text.Trim();
            if (def.Required && text.Length == 0)
                issues.Add($"参数「{def.DisplayName}」必填");
            else if (def.Kind == DriverParamKind.Int && text.Length > 0 && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                issues.Add($"参数「{def.DisplayName}」应为整数，当前填的是「{text}」");
            parameters[def.Key] = text;
        }

        var device = new DeviceDefinition { DeviceId = deviceId, DriverId = driverId, Parameters = parameters };

        // ---- 点位行 → DTO（数值解析失败先记问题，值以 0 占位让结构校验继续） ----
        var points = new List<PointDto>();
        foreach (var row in _rows)
        {
            if (!int.TryParse(row.ScanPeriodMs.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var scan))
            {
                issues.Add($"点位「{row.Id}」：周期(ms)「{row.ScanPeriodMs}」不是整数");
                scan = 0;
            }

            if (!int.TryParse(row.LengthWords.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var words))
            {
                issues.Add($"点位「{row.Id}」：字数「{row.LengthWords}」不是整数");
                words = 0;
            }

            points.Add(new PointDto
            {
                Id = row.Id.Trim(),
                DeviceId = deviceId,
                Address = row.Address.Trim(),
                DataType = row.DataType,
                ScanPeriodMs = scan,
                LengthWords = words,
            });
        }

        // ---- 结构校验 ----
        var probe = new ProjectDocument
        {
            Device = device,
            Points = points,
            UseEmbeddedSimulator = SimulatorCheck.IsChecked == true,
        };
        issues.AddRange(ProjectValidator.Validate(probe));

        // ---- 地址格式校验（驱动职责；结构问题未清时跳过，避免噪声叠加） ----
        if (issues.Count == 0)
        {
            try
            {
                var driver = ProjectMapper.CreateDriver(driverId);
                await using var session = driver.CreateSession(ProjectMapper.MapDevice(probe));
                foreach (var point in points)
                {
                    try
                    {
                        session.ParseAddress(point.Address);
                    }
                    catch (FormatException ex)
                    {
                        issues.Add($"点位「{point.Id}」：{ex.Message}");
                    }
                }
            }
            catch (NotSupportedException ex)
            {
                issues.Add(ex.Message);
            }
        }

        if (issues.Count > 0)
        {
            ErrorText.Text = "校验未通过，请修正后重试：\n" + string.Join("\n", issues.Take(8))
                + (issues.Count > 8 ? $"\n……另有 {issues.Count - 8} 条" : string.Empty);
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        EditedDevice = device;
        EditedPoints = points;
        EditedUseEmbeddedSimulator = probe.UseEmbeddedSimulator;
        DialogResult = true;
    }
}
