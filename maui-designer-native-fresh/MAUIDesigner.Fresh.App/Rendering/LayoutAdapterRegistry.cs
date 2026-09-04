using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Rendering;

public sealed class LayoutAdapterRegistry
{
    private readonly ILayoutAdapter[] _adapters =
    [
        new AbsoluteLayoutAdapter(),
        new GridLayoutAdapter(),
        new StackLayoutAdapter(),
        new GenericLayoutAdapter(),
        new ContentLayoutAdapter()
    ];

    public ILayoutAdapter Resolve(ControlDescriptor descriptor) =>
        _adapters.FirstOrDefault(adapter => adapter.CanHandle(descriptor))
        ?? throw new InvalidOperationException(
            $"No child layout adapter accepts '{descriptor.RuntimeType.FullName}'.");

    private sealed class AbsoluteLayoutAdapter : ILayoutAdapter
    {
        public bool CanHandle(ControlDescriptor descriptor) =>
            typeof(AbsoluteLayout).IsAssignableFrom(descriptor.RuntimeType);

        public void AddChild(View parent, View child, DesignerNode childNode)
        {
            var layout = (AbsoluteLayout)parent;
            RectD bounds = childNode.Bounds ?? new RectD(24, 24, 160, 48);
            AbsoluteLayout.SetLayoutBounds(child, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            AbsoluteLayout.SetLayoutFlags(child, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            layout.Children.Add(child);
        }

        public LayoutPlacement ResolveDrop(View parent, DesignerNode parentNode, PointD position) =>
            new(Bounds: new RectD(
                Math.Max(0, position.X),
                Math.Max(0, position.Y),
                160,
                48),
                PropertyUpdates: ClearGridPlacement());

        public View? AddDropPreview(View parent, LayoutPlacement placement)
        {
            if (parent is not AbsoluteLayout layout || placement.Bounds is not RectD bounds)
            {
                return null;
            }

            var preview = CreatePreview();
            AbsoluteLayout.SetLayoutBounds(preview, new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            layout.Children.Add(preview);
            return preview;
        }
    }

    private sealed class GridLayoutAdapter : ILayoutAdapter
    {
        public bool CanHandle(ControlDescriptor descriptor) =>
            typeof(Grid).IsAssignableFrom(descriptor.RuntimeType);

        public void AddChild(View parent, View child, DesignerNode childNode)
        {
            var grid = (Grid)parent;
            grid.Add(child);
            ApplyGridPlacement(child, childNode);
        }

        public LayoutPlacement ResolveDrop(View parent, DesignerNode parentNode, PointD position)
        {
            var grid = (Grid)parent;
            IReadOnlyList<double> rowHeights = ResolveTrackSizes(
                grid.RowDefinitions.Select(definition => definition.Height).ToArray(),
                grid.Height,
                GetAutoTrackSizes(grid, rows: true),
                grid.RowSpacing);
            IReadOnlyList<double> columnWidths = ResolveTrackSizes(
                grid.ColumnDefinitions.Select(definition => definition.Width).ToArray(),
                grid.Width,
                GetAutoTrackSizes(grid, rows: false),
                grid.ColumnSpacing);
            GridCell cell = GridGeometry.LocateCell(
                position,
                new RectD(0, 0, Math.Max(0, grid.Width), Math.Max(0, grid.Height)),
                rowHeights,
                columnWidths);
            return new LayoutPlacement(
                PropertyUpdates: ClearGridPlacement()
                    .SetItem("Grid.Row", DesignerValue.Literal(cell.Row.ToString(CultureInfo.InvariantCulture)))
                    .SetItem("Grid.Column", DesignerValue.Literal(cell.Column.ToString(CultureInfo.InvariantCulture))));
        }

        public View? AddDropPreview(View parent, LayoutPlacement placement)
        {
            if (parent is not Grid grid ||
                !TryReadPlacement(placement, "Grid.Row", out int row) ||
                !TryReadPlacement(placement, "Grid.Column", out int column))
            {
                return null;
            }

            Border preview = CreatePreview();
            Grid.SetRow(preview, row);
            Grid.SetColumn(preview, column);
            grid.Add(preview);
            return preview;
        }

        private static IReadOnlyList<double> ResolveTrackSizes(
            IReadOnlyList<GridLength> definitions,
            double available,
            IReadOnlyDictionary<int, double> measuredAutoSizes,
            double spacing)
        {
            if (definitions.Count == 0)
            {
                return [Math.Max(0, available)];
            }

            double fixedSize = definitions
                .Where(length => length.IsAbsolute)
                .Sum(length => length.Value);
            double autoSize = definitions
                .Select((length, index) => length.IsAuto
                    ? measuredAutoSizes.GetValueOrDefault(index)
                    : 0)
                .Sum();
            double starWeight = definitions
                .Where(length => length.IsStar)
                .Sum(length => Math.Max(0, length.Value));
            double totalSpacing = Math.Max(0, definitions.Count - 1) * Math.Max(0, spacing);
            double remaining = Math.Max(0, available - fixedSize - autoSize - totalSpacing);
            double starUnit = starWeight <= 0
                ? 0
                : remaining / starWeight;
            return definitions.Select((length, index) =>
            {
                double trackSize = length.IsAbsolute
                    ? Math.Max(0, length.Value)
                    : length.IsAuto
                        ? measuredAutoSizes.GetValueOrDefault(index)
                        : Math.Max(0, length.Value) * starUnit;
                return index < definitions.Count - 1
                    ? trackSize + Math.Max(0, spacing)
                    : trackSize;
            }).ToArray();
        }

        private static IReadOnlyDictionary<int, double> GetAutoTrackSizes(Grid grid, bool rows)
        {
            var sizes = new Dictionary<int, double>();
            foreach (IView child in grid.Children)
            {
                int span = rows ? Grid.GetRowSpan((BindableObject)child) : Grid.GetColumnSpan((BindableObject)child);
                if (span != 1)
                {
                    continue;
                }

                int index = rows ? Grid.GetRow((BindableObject)child) : Grid.GetColumn((BindableObject)child);
                double size = rows ? child.Frame.Height : child.Frame.Width;
                sizes[index] = Math.Max(sizes.GetValueOrDefault(index), Math.Max(0, size));
            }

            return sizes;
        }
    }

    private sealed class StackLayoutAdapter : ILayoutAdapter
    {
        public bool CanHandle(ControlDescriptor descriptor) =>
            typeof(StackBase).IsAssignableFrom(descriptor.RuntimeType);

        public void AddChild(View parent, View child, DesignerNode childNode) =>
            ((Layout)parent).Children.Add(child);

        public LayoutPlacement ResolveDrop(View parent, DesignerNode parentNode, PointD position)
        {
            var layout = (Layout)parent;
            bool horizontal = parent is HorizontalStackLayout;
            int index = layout.Children.Count;
            for (int childIndex = 0; childIndex < layout.Children.Count; childIndex++)
            {
                IView child = layout.Children[childIndex];
                double midpoint = horizontal
                    ? child.Frame.Left + child.Frame.Width / 2
                    : child.Frame.Top + child.Frame.Height / 2;
                double coordinate = horizontal ? position.X : position.Y;
                if (coordinate < midpoint)
                {
                    index = childIndex;
                    break;
                }
            }

            return new LayoutPlacement(index, PropertyUpdates: ClearGridPlacement());
        }

        public View? AddDropPreview(View parent, LayoutPlacement placement)
        {
            if (parent is not Layout layout)
            {
                return null;
            }

            var indicator = new BoxView
            {
                InputTransparent = true,
                Color = Color.FromArgb("#38BDF8"),
                HeightRequest = parent is HorizontalStackLayout ? -1 : 3,
                WidthRequest = parent is HorizontalStackLayout ? 3 : -1
            };
            int index = Math.Clamp(placement.DestinationIndex, 0, layout.Children.Count);
            layout.Children.Insert(index, indicator);
            return indicator;
        }
    }

    private sealed class GenericLayoutAdapter : ILayoutAdapter
    {
        public bool CanHandle(ControlDescriptor descriptor) =>
            typeof(Layout).IsAssignableFrom(descriptor.RuntimeType);

        public void AddChild(View parent, View child, DesignerNode childNode) =>
            ((Layout)parent).Children.Add(child);

        public LayoutPlacement ResolveDrop(View parent, DesignerNode parentNode, PointD position) =>
            new(PropertyUpdates: ClearGridPlacement());

        public View? AddDropPreview(View parent, LayoutPlacement placement)
        {
            return null;
        }
    }

    private sealed class ContentLayoutAdapter : ILayoutAdapter
    {
        public bool CanHandle(ControlDescriptor descriptor) =>
            VisualContentProperty.Find(descriptor.RuntimeType) is not null;

        public void AddChild(View parent, View child, DesignerNode childNode)
        {
            PropertyInfo property = VisualContentProperty.Find(parent.GetType())
                ?? throw new InvalidOperationException(
                    $"Visual content property for '{parent.GetType().FullName}' is unavailable.");
            property.SetValue(parent, child);
        }

        public LayoutPlacement ResolveDrop(View parent, DesignerNode parentNode, PointD position) =>
            new(PropertyUpdates: ClearGridPlacement());

        public View? AddDropPreview(View parent, LayoutPlacement placement)
        {
            return null;
        }
    }

    private static Border CreatePreview() =>
        new()
        {
            InputTransparent = true,
            BackgroundColor = Color.FromArgb("#3838BDF8"),
            Stroke = Color.FromArgb("#38BDF8"),
            StrokeThickness = 2,
            ZIndex = int.MaxValue
        };

    private static ImmutableDictionary<string, DesignerValue?> ClearGridPlacement() =>
        ImmutableDictionary<string, DesignerValue?>.Empty
            .Add("Grid.Row", null)
            .Add("Grid.Column", null)
            .Add("Grid.RowSpan", null)
            .Add("Grid.ColumnSpan", null);

    private static bool TryReadPlacement(
        LayoutPlacement placement,
        string propertyName,
        out int value)
    {
        value = 0;
        return placement.PropertyUpdates?.TryGetValue(propertyName, out DesignerValue? property) == true &&
            property is not null &&
            int.TryParse(property.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
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
            int.TryParse(property.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
