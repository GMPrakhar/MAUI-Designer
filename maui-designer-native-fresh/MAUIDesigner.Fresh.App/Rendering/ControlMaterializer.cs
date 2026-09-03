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
    private readonly DesignerWorkspace _workspace;

    public ControlMaterializer(IControlCatalog catalog, DesignerWorkspace workspace)
    {
        _catalog = catalog;
        _workspace = workspace;
    }

    public static string ToolboxControlPayload => ControlPayload;

    public View Materialize(DesignerDocument document) =>
        Build(document.Root, isRoot: true);

    private View Build(DesignerNode node, bool isRoot)
    {
        if (!_catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) || descriptor is null)
        {
            return CreateUnknownControl(node);
        }

        View view = descriptor.Factory(EmptyServiceProvider.Instance);
        view.AutomationId = $"designer-{node.Id.Value}";
        ApplyProperties(view, descriptor, node);
        foreach (DesignerNode childNode in node.Children)
        {
            AddChild(view, Build(childNode, isRoot: false), childNode);
        }

        EnsureDesignSize(view, descriptor);
        if (descriptor.AcceptsChildren)
        {
            AttachDropTarget(view, node.Id);
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

        bool selected = node.Id == _workspace.SelectedId;
        bool dropTarget = node.Id == _workspace.DropTargetId;
        chrome.Add(new Border
        {
            InputTransparent = true,
            Stroke = dropTarget
                ? Color.FromArgb("#38BDF8")
                : selected
                    ? Color.FromArgb("#7C5CFF")
                    : Colors.Transparent,
            StrokeThickness = selected || dropTarget ? (dropTarget ? 3 : 2) : 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(4)
            }
        });

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

        if (selected)
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

    private void AttachDropTarget(View view, ElementId parentId)
    {
        var drop = new DropGestureRecognizer { AllowDrop = true };
        drop.DragOver += (_, args) =>
        {
            if (HasDesignerPayload(args.Data.Properties))
            {
                _workspace.SetDropTarget(parentId);
            }
        };
        drop.DragLeave += (_, _) => _workspace.ClearDropTarget();
        drop.Drop += (_, args) =>
        {
            Point? point = args.GetPosition(view);
            PointD? position = point is null ? null : new PointD(point.Value.X, point.Value.Y);
            if (args.Data.Properties.TryGetValue(ControlPayload, out object? controlValue) &&
                controlValue is ControlDescriptor descriptor)
            {
                _workspace.Add(descriptor, parentId, position);
            }
            else if (args.Data.Properties.TryGetValue(ElementPayload, out object? elementValue) &&
                     elementValue is string id)
            {
                _workspace.Reparent(new ElementId(id), parentId, position);
            }

            _workspace.ClearDropTarget();
        };
        view.GestureRecognizers.Add(drop);
    }

    private static bool HasDesignerPayload(DataPackagePropertySet properties) =>
        properties.ContainsKey(ControlPayload) || properties.ContainsKey(ElementPayload);

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

    private static void AddChild(View parent, View child, DesignerNode childNode)
    {
        if (parent is AbsoluteLayout absoluteLayout)
        {
            RectD bounds = childNode.Bounds ?? new RectD(24, 24, 160, 48);
            AbsoluteLayout.SetLayoutBounds(child, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            AbsoluteLayout.SetLayoutFlags(child, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            absoluteLayout.Children.Add(child);
            return;
        }

        if (parent is Grid grid)
        {
            grid.Add(child);
            ApplyGridPlacement(child, childNode);
            return;
        }

        if (parent is Layout layout)
        {
            layout.Children.Add(child);
            return;
        }

        PropertyInfo? contentProperty = parent.GetType().GetProperty(
            "Content",
            BindingFlags.Public | BindingFlags.Instance);
        if (contentProperty?.CanWrite == true &&
            (contentProperty.PropertyType.IsInstanceOfType(child) ||
             contentProperty.PropertyType == typeof(object)))
        {
            contentProperty.SetValue(parent, child);
        }
    }

    private static void ApplyGridPlacement(View child, DesignerNode node)
    {
        if (TryReadInt(node, "Grid.Row", out int row))
        {
            Grid.SetRow(child, row);
        }

        if (TryReadInt(node, "Grid.Column", out int column))
        {
            Grid.SetColumn(child, column);
        }

        if (TryReadInt(node, "Grid.RowSpan", out int rowSpan))
        {
            Grid.SetRowSpan(child, Math.Max(1, rowSpan));
        }

        if (TryReadInt(node, "Grid.ColumnSpan", out int columnSpan))
        {
            Grid.SetColumnSpan(child, Math.Max(1, columnSpan));
        }
    }

    private static bool TryReadInt(DesignerNode node, string name, out int value)
    {
        value = 0;
        return node.Properties.TryGetValue(name, out DesignerValue? property) &&
            int.TryParse(
                property.Text,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
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
