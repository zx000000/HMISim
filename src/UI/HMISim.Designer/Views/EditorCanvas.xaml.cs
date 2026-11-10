using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HMISim.Common;
using HMISim.Controls;
using HMISim.ProjectModel;

namespace HMISim.Designer.Views;

/// <summary>
/// 组态画布控件：把工程画面的控件渲染为可编辑块——单击选中、拖动移动、右下角手柄缩放、点空白处取消选中。
/// 几何变更直接写回 <see cref="ControlItem"/> 并通过 <see cref="GeometryChanged"/> 通知宿主刷新属性面板。
/// </summary>
public partial class EditorCanvas : UserControl
{
    private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x25, 0x63, 0xEB));
    private static readonly Brush HandleBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA0, 0xA6));

    private readonly Dictionary<ControlItem, Border> _visuals = [];
    private ProjectDocument? _document;
    private ControlItem? _selected;
    private ControlItem? _dragItem;
    private bool _resizing;
    private Point _dragStart;
    private double _origX, _origY, _origWidth, _origHeight;

    /// <summary>选中项变化事件（参数为新选中项，null 表示取消选中）。</summary>
    public event Action<ControlItem?>? SelectionChanged;

    /// <summary>控件几何被拖动/缩放修改后的事件。</summary>
    public event Action<ControlItem>? GeometryChanged;

    /// <summary>创建画布。</summary>
    public EditorCanvas() => InitializeComponent();

    /// <summary>当前选中项。</summary>
    public ControlItem? SelectedItem => _selected;

    /// <summary>装载一份工程文档并重绘。</summary>
    public void SetDocument(ProjectDocument? document)
    {
        _document = document;
        _selected = null;
        Rebuild();
        SelectionChanged?.Invoke(null);
    }

    /// <summary>按当前文档全量重绘（增删控件后调用）。</summary>
    public void Rebuild()
    {
        Surface.Children.Clear();
        _visuals.Clear();
        if (_document is null)
            return;
        Surface.Width = _document.Screen.Width;
        Surface.Height = _document.Screen.Height;
        foreach (var item in _document.Screen.Controls)
            AddVisual(item);
        ApplySelectionVisual();
    }

    /// <summary>刷新单个控件的呈现（属性面板改了标题/绑定/几何后调用）。</summary>
    public void RefreshItem(ControlItem item)
    {
        if (_visuals.TryGetValue(item, out var old))
        {
            Surface.Children.Remove(old);
            _visuals.Remove(item);
        }

        AddVisual(item);
        ApplySelectionVisual();
    }

    /// <summary>程序化选中某控件（工具箱新增后调用）。</summary>
    public void Select(ControlItem? item)
    {
        _selected = item;
        ApplySelectionVisual();
        SelectionChanged?.Invoke(item);
    }

    private void AddVisual(ControlItem item)
    {
        UIElement preview = item.Kind == ControlKind.ValueDisplay
            ? new ValueDisplay
            {
                Title = item.Title,
                DisplayValue = "88.88",
                Quality = Quality.Good,
                IsHitTestVisible = false,
            }
            : new TextBlock
            {
                Text = item.Title,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x1F, 0x29, 0x37)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };
        var grid = new Grid();
        grid.Children.Add(preview);
        var handle = new Rectangle
        {
            Width = 12,
            Height = 12,
            Fill = HandleBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNWSE,
            Tag = item,
        };
        handle.MouseLeftButtonDown += OnHandleMouseDown;
        grid.Children.Add(handle);

        var border = new Border
        {
            Width = item.Width,
            Height = item.Height,
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.White,
            Child = grid,
            Cursor = Cursors.SizeAll,
            Tag = item,
        };
        border.MouseLeftButtonDown += OnItemMouseDown;
        Canvas.SetLeft(border, item.X);
        Canvas.SetTop(border, item.Y);
        Surface.Children.Add(border);
        _visuals[item] = border;
    }

    private void ApplySelectionVisual()
    {
        foreach (var (item, border) in _visuals)
            border.BorderBrush = item == _selected ? SelectedBrush : Brushes.Transparent;
    }

    private void OnItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: ControlItem item })
            return;
        Select(item);
        BeginDrag(item, resizing: false, e);
        e.Handled = true;
    }

    private void OnHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle { Tag: ControlItem item })
            return;
        Select(item);
        BeginDrag(item, resizing: true, e);
        e.Handled = true;
    }

    private void BeginDrag(ControlItem item, bool resizing, MouseButtonEventArgs e)
    {
        _dragItem = item;
        _resizing = resizing;
        _dragStart = e.GetPosition(Surface);
        _origX = item.X;
        _origY = item.Y;
        _origWidth = item.Width;
        _origHeight = item.Height;
        Surface.CaptureMouse();
    }

    private void Surface_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        var pos = e.GetPosition(Surface);
        var dx = pos.X - _dragStart.X;
        var dy = pos.Y - _dragStart.Y;
        if (_resizing)
        {
            _dragItem.Width = Math.Max(60, _origWidth + dx);
            _dragItem.Height = Math.Max(36, _origHeight + dy);
        }
        else
        {
            _dragItem.X = Math.Max(0, _origX + dx);
            _dragItem.Y = Math.Max(0, _origY + dy);
        }

        if (_visuals.TryGetValue(_dragItem, out var border))
        {
            border.Width = _dragItem.Width;
            border.Height = _dragItem.Height;
            Canvas.SetLeft(border, _dragItem.X);
            Canvas.SetTop(border, _dragItem.Y);
        }
    }

    private void Surface_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragItem is null)
            return;
        var item = _dragItem;
        _dragItem = null;
        Surface.ReleaseMouseCapture();
        GeometryChanged?.Invoke(item);
    }

    private void Surface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == Surface)
            Select(null);
    }
}
