namespace MAUIDesigner.Fresh.App.Controls;

public static class SidebarResizePolicy
{
    public const double MinimumWidth = 180;
    public const double MaximumWidth = 560;

    public static double CalculateWidth(
        double startingWidth,
        double totalHorizontalChange,
        bool resizeFromRightEdge)
    {
        double delta = resizeFromRightEdge
            ? totalHorizontalChange
            : -totalHorizontalChange;
        return Math.Clamp(startingWidth + delta, MinimumWidth, MaximumWidth);
    }
}
