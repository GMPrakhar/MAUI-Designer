namespace MAUIDesigner.Fresh.Core.Geometry;

public readonly record struct GridCell(int Row, int Column);

public static class GridGeometry
{
    public static GridCell LocateCell(
        PointD point,
        RectD gridBounds,
        IReadOnlyList<double> rowHeights,
        IReadOnlyList<double> columnWidths)
    {
        ArgumentNullException.ThrowIfNull(rowHeights);
        ArgumentNullException.ThrowIfNull(columnWidths);
        if (rowHeights.Count == 0 || columnWidths.Count == 0)
        {
            throw new ArgumentException("Grid geometry requires at least one row and one column.");
        }

        double localX = Math.Clamp(point.X - gridBounds.X, 0, Math.Max(0, gridBounds.Width));
        double localY = Math.Clamp(point.Y - gridBounds.Y, 0, Math.Max(0, gridBounds.Height));
        return new GridCell(
            FindTrack(localY, rowHeights),
            FindTrack(localX, columnWidths));
    }

    private static int FindTrack(double offset, IReadOnlyList<double> sizes)
    {
        double edge = 0;
        for (int index = 0; index < sizes.Count; index++)
        {
            double size = sizes[index];
            if (double.IsNaN(size) || double.IsInfinity(size) || size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizes), "Track sizes must be finite and non-negative.");
            }

            edge += size;
            if (offset < edge || index == sizes.Count - 1)
            {
                return index;
            }
        }

        return sizes.Count - 1;
    }
}
