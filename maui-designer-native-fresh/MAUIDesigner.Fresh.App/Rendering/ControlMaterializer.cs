using System.Reflection;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Rendering;

public sealed class ControlMaterializer
{
    private readonly IControlCatalog _catalog;
    private readonly Dictionary<ElementId, Border> _outlines = [];
    private readonly Dictionary<ElementId, (View View, ILayoutAdapter Adapter)> _targets = [];
    private readonly LayoutAdapterRegistry _layoutAdapters = new();
    private readonly DesignerWorkspace _workspace;
    private View? _activeDropPreview;
    private ManualDragState? _manualDrag;

    public ControlMaterializer(IControlCatalog catalog, DesignerWorkspace workspace)
    {
        _catalog = catalog;
        _workspace = workspace;
    }

    public View Materialize(DesignerDocument document)
    {
        _outlines.Clear();
        _targets.Clear();
        _activeDropPreview = null;
        _manualDrag = null;
        return Build(document.Root, isRoot: true);
    }

    public void UpdateInteraction()
    {
        RemoveActiveDropPreview();
        foreach ((ElementId id, Border outline) in _outlines)
        {
            UpdateOutline(outline, id);
        }

        if (_workspace.DropTargetId is not ElementId targetId ||
            _workspace.DropPlacement is not LayoutPlacement placement ||
            !_targets.TryGetValue(targetId, out (View View, ILayoutAdapter Adapter) target))
        {
            return;
        }

        _activeDropPreview = target.Adapter.AddDropPreview(target.View, placement);
    }

    public void BeginManualDrag(View source, ElementId? movingId)
    {
        if (!TryGetWindowBounds(source, out RectD sourceBounds))
        {
            _manualDrag = null;
            return;
        }

        ManualDropTarget[] targets = _targets
            .Where(target => _workspace.CanAcceptChild(target.Key, movingId))
            .Select(target => TryGetWindowBounds(target.Value.View, out RectD bounds)
                ? new ManualDropTarget(
                    target.Key,
                    target.Value.View,
                    target.Value.Adapter,
                    bounds)
                : null)
            .OfType<ManualDropTarget>()
            .OrderBy(target => target.Bounds.Area)
            .ToArray();
        _manualDrag = new ManualDragState(
            new PointD(
                sourceBounds.X + sourceBounds.Width / 2,
                sourceBounds.Y + sourceBounds.Height / 2),
            targets);
    }

    public ElementId? UpdateManualDrag(double totalX, double totalY)
    {
        if (_manualDrag is not ManualDragState drag)
        {
            return null;
        }

        var pointer = new PointD(drag.Start.X + totalX, drag.Start.Y + totalY);
        ManualDropTarget? target = drag.Targets.FirstOrDefault(candidate =>
            candidate.Bounds.Contains(pointer));
        DesignerNode? parentNode = target is null
            ? null
            : _workspace.Session.Current.Find(target.Id);
        if (target is null || parentNode is null)
        {
            _workspace.ClearDropTarget();
            return null;
        }

        LayoutPlacement placement = target.Adapter.ResolveDrop(
            target.View,
            parentNode,
            new PointD(
                pointer.X - target.Bounds.X,
                pointer.Y - target.Bounds.Y));
        _workspace.SetDropTarget(target.Id, placement);
        return target.Id;
    }

    public void CompleteManualToolboxDrag(ControlDescriptor descriptor)
    {
        _manualDrag = null;
        if (_workspace.DropTargetId is ElementId targetId &&
            _workspace.DropPlacement is LayoutPlacement placement)
        {
            _workspace.Add(descriptor, targetId, placement);
        }

        _workspace.ClearDropTarget();
    }

    public void CancelManualDrag()
    {
        _manualDrag = null;
        _workspace.ClearDropTarget();
    }

    private View Build(DesignerNode node, bool isRoot)
    {
        if (!_catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) || descriptor is null)
        {
            return CreateUnknownControl(node);
        }

        View view = descriptor.Factory(EmptyServiceProvider.Instance);
        view.AutomationId = $"designer-{node.Id.Value}";
        ApplyProperties(view, descriptor, node);
        ILayoutAdapter? layoutAdapter = descriptor.AcceptsChildren
            ? _layoutAdapters.Resolve(descriptor)
            : null;
        foreach (DesignerNode childNode in node.Children)
        {
            layoutAdapter!.AddChild(view, Build(childNode, isRoot: false), childNode);
        }

        EnsureDesignSize(view, descriptor);
        if (layoutAdapter is not null)
        {
            _targets[node.Id] = (view, layoutAdapter);
        }

        if (isRoot)
        {
            return view;
        }

        return CreateChrome(view, node);
    }

    private View CreateChrome(View content, DesignerNode node)
    {
        var chrome = new Grid
        {
            MinimumWidthRequest = 24,
            MinimumHeightRequest = 24,
            AutomationId = $"chrome-{node.Id.Value}"
        };
        chrome.Add(content);

        var outline = new Border
        {
            InputTransparent = true,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(4)
            }
        };
        UpdateOutline(outline, node.Id);
        _outlines[node.Id] = outline;
        chrome.Add(outline);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _workspace.Select(node.Id);
        chrome.GestureRecognizers.Add(tap);

        var reparent = new PanGestureRecognizer();
        reparent.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Started)
            {
                BeginManualDrag(chrome, node.Id);
            }
            else if (args.StatusType == GestureStatus.Running)
            {
                _ = UpdateManualDrag(args.TotalX, args.TotalY);
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                _manualDrag = null;
                if (_workspace.DropTargetId is ElementId targetId &&
                    _workspace.DropPlacement is LayoutPlacement placement)
                {
                    _workspace.Reparent(node.Id, targetId, placement);
                }
                else
                {
                    _workspace.ClearDropTarget();
                }
            }
            else if (args.StatusType == GestureStatus.Canceled)
            {
                CancelManualDrag();
            }
        };
        chrome.GestureRecognizers.Add(reparent);

        if (node.Id == _workspace.SelectedId)
        {
            AddMoveHandle(chrome, node);
            AddResizeHandle(chrome, node);
        }

        return chrome;
    }

    private void AddMoveHandle(Grid chrome, DesignerNode node)
    {
        var handle = new Border
        {
            AutomationId = $"move-{node.Id.Value}",
            WidthRequest = 28,
            HeightRequest = 14,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = -4,
            TranslationY = -18,
            BackgroundColor = Color.FromArgb("#7C5CFF"),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(5)
            },
            Content = new Label
            {
                InputTransparent = true,
                Text = ":::",
                FontSize = 9,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }
        };
        RectD start = node.Bounds ?? new RectD(0, 0, 160, 48);
        double totalX = 0;
        double totalY = 0;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Running)
            {
                totalX = args.TotalX;
                totalY = args.TotalY;
                chrome.TranslationX = totalX;
                chrome.TranslationY = totalY;
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                chrome.TranslationX = 0;
                chrome.TranslationY = 0;
                _workspace.SetBounds(node.Id, start with
                {
                    X = Math.Max(0, start.X + totalX),
                    Y = Math.Max(0, start.Y + totalY)
                });
            }
            else if (args.StatusType == GestureStatus.Canceled)
            {
                chrome.TranslationX = 0;
                chrome.TranslationY = 0;
            }
        };
        handle.GestureRecognizers.Add(pan);
        chrome.Add(handle);
    }

    private void AddResizeHandle(Grid chrome, DesignerNode node)
    {
        var handle = new Border
        {
            AutomationId = $"resize-{node.Id.Value}",
            WidthRequest = 12,
            HeightRequest = 12,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            TranslationX = 6,
            TranslationY = 6,
            BackgroundColor = Color.FromArgb("#7C5CFF"),
            Stroke = Colors.White,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(6)
            }
        };
        RectD start = node.Bounds ?? new RectD(0, 0, 160, 48);
        double totalX = 0;
        double totalY = 0;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Running)
            {
                totalX = args.TotalX;
                totalY = args.TotalY;
                double width = Math.Max(24, start.Width + totalX);
                double height = Math.Max(24, start.Height + totalY);
                chrome.WidthRequest = width;
                chrome.HeightRequest = height;
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                double width = Math.Max(24, start.Width + totalX);
                double height = Math.Max(24, start.Height + totalY);
                _workspace.SetBounds(node.Id, start with { Width = width, Height = height });
            }
            else if (args.StatusType == GestureStatus.Canceled)
            {
                chrome.WidthRequest = start.Width;
                chrome.HeightRequest = start.Height;
            }
        };
        handle.GestureRecognizers.Add(pan);
        chrome.Add(handle);
    }

    private static bool TryGetWindowBounds(View view, out RectD bounds)
    {
#if WINDOWS
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native &&
            native.XamlRoot is not null)
        {
            Windows.Foundation.Point origin = native
                .TransformToVisual(null)
                .TransformPoint(new Windows.Foundation.Point());
            bounds = new RectD(origin.X, origin.Y, native.ActualWidth, native.ActualHeight);
            return bounds.Width > 0 && bounds.Height > 0;
        }
#endif
        bounds = default;
        return false;
    }

    private void UpdateOutline(Border outline, ElementId id)
    {
        bool selected = id == _workspace.SelectedId;
        bool dropTarget = id == _workspace.DropTargetId;
        outline.Stroke = dropTarget
            ? Color.FromArgb("#38BDF8")
            : selected
                ? Color.FromArgb("#7C5CFF")
                : Colors.Transparent;
        outline.StrokeThickness = dropTarget ? 3 : selected ? 2 : 0;
    }

    private void RemoveActiveDropPreview()
    {
        if (_activeDropPreview?.Parent is Layout layout)
        {
            layout.Children.Remove(_activeDropPreview);
        }

        _activeDropPreview = null;
    }

    private static void ApplyProperties(
        View view,
        ControlDescriptor descriptor,
        DesignerNode node)
    {
        foreach ((string name, DesignerValue designerValue) in node.Properties)
        {
            if (designerValue.Kind != DesignerValueKind.Literal)
            {
                continue;
            }

            PropertyDescriptor? propertyDescriptor = descriptor.Properties
                .FirstOrDefault(property => property.Name == name && !property.IsReadOnly);
            PropertyInfo? property = propertyDescriptor is null
                ? null
                : descriptor.RuntimeType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property is null ||
                !DesignerValueConverter.TryConvert(designerValue.Text, property.PropertyType, out object? value))
            {
                continue;
            }

            property.SetValue(view, value);
        }

        PropertyInfo? textProperty = descriptor.RuntimeType.GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
        if (!node.Properties.ContainsKey("Text") && textProperty?.CanWrite == true && textProperty.PropertyType == typeof(string))
        {
            textProperty.SetValue(view, descriptor.DisplayName);
        }

        ApplyPreviewTextContrast(view, descriptor, node);
    }

    private static void ApplyPreviewTextContrast(
        View view,
        ControlDescriptor descriptor,
        DesignerNode node)
    {
        if (node.Properties.ContainsKey("TextColor") ||
            view.BackgroundColor is Color background && background.Alpha > 0.05f)
        {
            return;
        }

        PropertyDescriptor? textColorDescriptor = descriptor.Properties
            .FirstOrDefault(property =>
                property.Name == "TextColor" &&
                !property.IsReadOnly &&
                property.ValueType == typeof(Color));
        PropertyInfo? textColorProperty = textColorDescriptor is null
            ? null
            : descriptor.RuntimeType.GetProperty(
                textColorDescriptor.Name,
                BindingFlags.Public | BindingFlags.Instance);
        textColorProperty?.SetValue(view, Colors.Black);
    }

    private static void EnsureDesignSize(View view, ControlDescriptor descriptor)
    {
        if (!descriptor.AcceptsChildren)
        {
            return;
        }

        view.MinimumWidthRequest = Math.Max(120, view.MinimumWidthRequest);
        view.MinimumHeightRequest = Math.Max(80, view.MinimumHeightRequest);
    }

    private static View CreateUnknownControl(DesignerNode node) =>
        new Border
        {
            Padding = 12,
            BackgroundColor = Color.FromArgb("#FFF3CD"),
            Stroke = Color.FromArgb("#D39E00"),
            Content = new Label
            {
                Text = $"Unavailable: {node.ControlType.XamlName}",
                TextColor = Color.FromArgb("#664D03")
            }
        };

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    private sealed record ManualDropTarget(
        ElementId Id,
        View View,
        ILayoutAdapter Adapter,
        RectD Bounds);

    private sealed record ManualDragState(
        PointD Start,
        IReadOnlyList<ManualDropTarget> Targets);
}
