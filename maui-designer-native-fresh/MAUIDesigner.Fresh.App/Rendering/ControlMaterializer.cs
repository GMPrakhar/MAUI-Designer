using System.Reflection;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Rendering;

public sealed class ControlMaterializer
{
    private const string ControlPayload = "maui-designer/control";
    private const string ElementPayload = "maui-designer/element";
    private readonly IControlCatalog _catalog;
    private readonly Dictionary<ElementId, Border> _outlines = [];
    private readonly Dictionary<ElementId, (View View, ILayoutAdapter Adapter)> _targets = [];
    private readonly LayoutAdapterRegistry _layoutAdapters = new();
    private readonly DesignerWorkspace _workspace;
    private View? _activeDropPreview;

    public ControlMaterializer(IControlCatalog catalog, DesignerWorkspace workspace)
    {
        _catalog = catalog;
        _workspace = workspace;
    }

    public static string ToolboxControlPayload => ControlPayload;

    public View Materialize(DesignerDocument document)
    {
        _outlines.Clear();
        _targets.Clear();
        _activeDropPreview = null;
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
            AttachDropTarget(view, node, layoutAdapter);
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

        var drag = new DragGestureRecognizer();
        drag.DragStarting += (_, args) =>
        {
            _workspace.Select(node.Id);
            args.Data.Properties[ElementPayload] = node.Id.Value;
        };
        chrome.GestureRecognizers.Add(drag);

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
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Running)
            {
                chrome.TranslationX = args.TotalX;
                chrome.TranslationY = args.TotalY;
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                chrome.TranslationX = 0;
                chrome.TranslationY = 0;
                _workspace.SetBounds(node.Id, start with
                {
                    X = Math.Max(0, start.X + args.TotalX),
                    Y = Math.Max(0, start.Y + args.TotalY)
                });
            }
        };
        handle.GestureRecognizers.Add(pan);
        chrome.Add(handle);
    }

    private void AddResizeHandle(Grid chrome, DesignerNode node)
    {
        var handle = new Border
        {
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
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) =>
        {
            double width = Math.Max(24, start.Width + args.TotalX);
            double height = Math.Max(24, start.Height + args.TotalY);
            if (args.StatusType == GestureStatus.Running)
            {
                chrome.WidthRequest = width;
                chrome.HeightRequest = height;
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                _workspace.SetBounds(node.Id, start with { Width = width, Height = height });
            }
        };
        handle.GestureRecognizers.Add(pan);
        chrome.Add(handle);
    }

    private void AttachDropTarget(
        View view,
        DesignerNode parentNode,
        ILayoutAdapter layoutAdapter)
    {
        var drop = new DropGestureRecognizer { AllowDrop = true };
        drop.DragOver += (_, args) =>
        {
            ElementId? movingId = TryGetMovingId(args.Data.Properties);
            if (HasDesignerPayload(args.Data.Properties) &&
                _workspace.CanAcceptChild(parentNode.Id, movingId) &&
                args.GetPosition(view) is Point point)
            {
                LayoutPlacement placement = layoutAdapter.ResolveDrop(
                    view,
                    parentNode,
                    new PointD(point.X, point.Y));
                _workspace.SetDropTarget(parentNode.Id, placement);
            }
        };
        drop.DragLeave += (_, _) => _workspace.ClearDropTarget();
        drop.Drop += (_, args) =>
        {
            Point? point = args.GetPosition(view);
            LayoutPlacement placement = parentNode.Id == _workspace.DropTargetId &&
                _workspace.DropPlacement is not null
                    ? _workspace.DropPlacement
                    : layoutAdapter.ResolveDrop(
                        view,
                        parentNode,
                        new PointD(point?.X ?? 0, point?.Y ?? 0));
            if (args.Data.Properties.TryGetValue(ControlPayload, out object? controlValue) &&
                controlValue is string controlKey &&
                _catalog.Controls.FirstOrDefault(candidate => candidate.Id.Key == controlKey) is
                    ControlDescriptor descriptor)
            {
                _workspace.Add(descriptor, parentNode.Id, placement);
            }
            else if (args.Data.Properties.TryGetValue(ElementPayload, out object? elementValue) &&
                     elementValue is string id)
            {
                _workspace.Reparent(new ElementId(id), parentNode.Id, placement);
            }

            _workspace.ClearDropTarget();
        };
        view.GestureRecognizers.Add(drop);
    }

    private static bool HasDesignerPayload(DataPackagePropertySet properties) =>
        properties.ContainsKey(ControlPayload) || properties.ContainsKey(ElementPayload);

    private static ElementId? TryGetMovingId(DataPackagePropertySet properties) =>
        properties.TryGetValue(ElementPayload, out object? value) && value is string id
            ? new ElementId(id)
            : null;

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
}
