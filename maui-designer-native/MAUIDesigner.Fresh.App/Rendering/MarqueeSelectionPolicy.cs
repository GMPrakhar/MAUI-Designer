using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Rendering;

public static class MarqueeSelectionPolicy
{
    public const double MinimumDragDistance = 4;

    public static bool Contains(RectD marquee, RectD element) =>
        marquee.Width >= MinimumDragDistance &&
        marquee.Height >= MinimumDragDistance &&
        element.X >= marquee.X &&
        element.Y >= marquee.Y &&
        element.Right <= marquee.Right &&
        element.Bottom <= marquee.Bottom;
}
