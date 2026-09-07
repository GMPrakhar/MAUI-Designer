using MAUIDesigner.Fresh.App.Viewport;
using Microsoft.Maui.Graphics;

namespace MAUIDesigner.Fresh.App.Rendering;

public sealed class CanvasGridDrawable : IDrawable
{
    private readonly DesignerViewportState _viewport;

    public CanvasGridDrawable(DesignerViewportState viewport)
    {
        _viewport = viewport;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_viewport.ShowGrid)
        {
            return;
        }

        canvas.StrokeColor = Color.FromArgb("#D9DEE8");
        canvas.StrokeSize = (float)(1 / _viewport.Zoom);
        for (double x = 0; x <= _viewport.DesignWidth; x += _viewport.GridSize)
        {
            canvas.DrawLine((float)x, 0, (float)x, (float)_viewport.DesignHeight);
        }

        for (double y = 0; y <= _viewport.DesignHeight; y += _viewport.GridSize)
        {
            canvas.DrawLine(0, (float)y, (float)_viewport.DesignWidth, (float)y);
        }
    }
}

public readonly record struct GridTrackLine(float X1, float Y1, float X2, float Y2);

public sealed class GridTrackOverlayDrawable : IDrawable
{
    private static readonly Color TrackColor = Color.FromArgb("#8B5CF6");
    private GridTrackLine[] _lines = [];
    private readonly float[] _dashPattern = new float[2];
    private int _lineCount;
    private float _strokeSize = 1;

    public int LineCount => _lineCount;

    public GridTrackLine GetLine(int index)
    {
        if ((uint)index >= (uint)_lineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _lines[index];
    }

    public void Update(Grid grid, RectF bounds, double deviceZoom)
    {
        ArgumentNullException.ThrowIfNull(grid);
        int rowCount = grid.RowDefinitions.Count;
        int columnCount = grid.ColumnDefinitions.Count;
        EnsureCapacity(Math.Max(0, rowCount - 1) + Math.Max(0, columnCount - 1));

        _lineCount = 0;
        double rowStarUnit = ResolveStarUnit(grid, rows: true);
        float y = bounds.Top;
        for (int index = 0; index < rowCount - 1; index++)
        {
            y += (float)ResolveRowHeight(grid, index, rowStarUnit);
            float lineY = y + (float)(Math.Max(0, grid.RowSpacing) / 2);
            _lines[_lineCount++] = new GridTrackLine(
                bounds.Left,
                lineY,
                bounds.Right,
                lineY);
            y += (float)Math.Max(0, grid.RowSpacing);
        }

        double columnStarUnit = ResolveStarUnit(grid, rows: false);
        float x = bounds.Left;
        for (int index = 0; index < columnCount - 1; index++)
        {
            x += (float)ResolveColumnWidth(grid, index, columnStarUnit);
            float lineX = x + (float)(Math.Max(0, grid.ColumnSpacing) / 2);
            _lines[_lineCount++] = new GridTrackLine(
                lineX,
                bounds.Top,
                lineX,
                bounds.Bottom);
            x += (float)Math.Max(0, grid.ColumnSpacing);
        }

        SetZoom(deviceZoom);
    }

    public void UpdateActualTracks(
        ReadOnlySpan<double> rowHeights,
        ReadOnlySpan<double> columnWidths,
        RectF bounds,
        double rowSpacing,
        double columnSpacing,
        double deviceZoom)
    {
        EnsureCapacity(Math.Max(0, rowHeights.Length - 1) +
            Math.Max(0, columnWidths.Length - 1));
        _lineCount = 0;

        float y = bounds.Top;
        for (int index = 0; index < rowHeights.Length - 1; index++)
        {
            y += (float)Math.Max(0, rowHeights[index]);
            float lineY = y + (float)(Math.Max(0, rowSpacing) / 2);
            _lines[_lineCount++] = new GridTrackLine(
                bounds.Left,
                lineY,
                bounds.Right,
                lineY);
            y += (float)Math.Max(0, rowSpacing);
        }

        float x = bounds.Left;
        for (int index = 0; index < columnWidths.Length - 1; index++)
        {
            x += (float)Math.Max(0, columnWidths[index]);
            float lineX = x + (float)(Math.Max(0, columnSpacing) / 2);
            _lines[_lineCount++] = new GridTrackLine(
                lineX,
                bounds.Top,
                lineX,
                bounds.Bottom);
            x += (float)Math.Max(0, columnSpacing);
        }

        SetZoom(deviceZoom);
    }

    public void Clear() => _lineCount = 0;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_lineCount == 0)
        {
            return;
        }

        canvas.StrokeColor = TrackColor;
        canvas.StrokeSize = _strokeSize;
        canvas.StrokeDashPattern = _dashPattern;
        canvas.StrokeLineCap = LineCap.Round;
        for (int index = 0; index < _lineCount; index++)
        {
            GridTrackLine line = _lines[index];
            canvas.DrawLine(line.X1, line.Y1, line.X2, line.Y2);
        }
    }

    private void EnsureCapacity(int required)
    {
        if (_lines.Length >= required)
        {
            return;
        }

        Array.Resize(ref _lines, Math.Max(required, _lines.Length == 0 ? 4 : _lines.Length * 2));
    }

    private void SetZoom(double deviceZoom)
    {
        double safeZoom = double.IsFinite(deviceZoom) && deviceZoom > 0 ? deviceZoom : 1;
        _strokeSize = (float)(1 / safeZoom);
        _dashPattern[0] = (float)(1.5 / safeZoom);
        _dashPattern[1] = (float)(3 / safeZoom);
    }

    private static double ResolveStarUnit(Grid grid, bool rows)
    {
        int count = rows ? grid.RowDefinitions.Count : grid.ColumnDefinitions.Count;
        double spacing = Math.Max(0, rows ? grid.RowSpacing : grid.ColumnSpacing);
        double available = Math.Max(0, rows ? grid.Height : grid.Width) -
            Math.Max(0, count - 1) * spacing;
        double consumed = 0;
        double starWeight = 0;
        for (int index = 0; index < count; index++)
        {
            GridLength length = rows
                ? grid.RowDefinitions[index].Height
                : grid.ColumnDefinitions[index].Width;
            if (length.IsAbsolute)
            {
                consumed += Math.Max(0, length.Value);
            }
            else if (length.IsAuto)
            {
                consumed += ResolveAutoSize(grid, rows, index);
            }
            else
            {
                starWeight += Math.Max(0, length.Value);
            }
        }

        return starWeight == 0 ? 0 : Math.Max(0, available - consumed) / starWeight;
    }

    private static double ResolveRowHeight(Grid grid, int index, double starUnit)
    {
        GridLength length = grid.RowDefinitions[index].Height;
        return length.IsAbsolute
            ? Math.Max(0, length.Value)
            : length.IsAuto
                ? ResolveAutoSize(grid, rows: true, index)
                : Math.Max(0, length.Value) * starUnit;
    }

    private static double ResolveColumnWidth(Grid grid, int index, double starUnit)
    {
        GridLength length = grid.ColumnDefinitions[index].Width;
        return length.IsAbsolute
            ? Math.Max(0, length.Value)
            : length.IsAuto
                ? ResolveAutoSize(grid, rows: false, index)
                : Math.Max(0, length.Value) * starUnit;
    }

    private static double ResolveAutoSize(Grid grid, bool rows, int index)
    {
        double size = 0;
        foreach (IView child in grid.Children)
        {
            var bindable = (BindableObject)child;
            int childIndex = rows ? Grid.GetRow(bindable) : Grid.GetColumn(bindable);
            int span = rows ? Grid.GetRowSpan(bindable) : Grid.GetColumnSpan(bindable);
            if (childIndex == index && span == 1)
            {
                size = Math.Max(size, rows ? child.Frame.Height : child.Frame.Width);
            }
        }

        return size;
    }
}

public sealed class CanvasRulerDrawable : IDrawable
{
    private const float RulerSize = 22;
    private readonly DesignerViewportState _viewport;

    public CanvasRulerDrawable(DesignerViewportState viewport)
    {
        _viewport = viewport;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (!_viewport.ShowRulers)
        {
            return;
        }

        canvas.FillColor = Color.FromArgb("#F8FAFC");
        canvas.FillRectangle(0, 0, dirtyRect.Width, RulerSize);
        canvas.FillRectangle(0, 0, RulerSize, dirtyRect.Height);
        canvas.StrokeColor = Color.FromArgb("#CBD5E1");
        canvas.FontColor = Color.FromArgb("#475569");
        canvas.FontSize = 8;

        double step = Math.Max(_viewport.GridSize * 5, 40);
        for (double value = 0; value <= _viewport.DesignWidth; value += step)
        {
            float x = (float)(_viewport.PanX + value * _viewport.Zoom);
            if (x < RulerSize || x > dirtyRect.Width)
            {
                continue;
            }

            canvas.DrawLine(x, 14, x, RulerSize);
            canvas.DrawString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x + 2,
                0,
                42,
                14,
                HorizontalAlignment.Left,
                VerticalAlignment.Center);
        }

        for (double value = 0; value <= _viewport.DesignHeight; value += step)
        {
            float y = (float)(_viewport.PanY + value * _viewport.Zoom);
            if (y < RulerSize || y > dirtyRect.Height)
            {
                continue;
            }

            canvas.DrawLine(14, y, RulerSize, y);
            canvas.DrawString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                1,
                y + 1,
                20,
                12,
                HorizontalAlignment.Left,
                VerticalAlignment.Top);
        }

        canvas.FillColor = Color.FromArgb("#EEF2F7");
        canvas.FillRectangle(0, 0, RulerSize, RulerSize);
    }
}
