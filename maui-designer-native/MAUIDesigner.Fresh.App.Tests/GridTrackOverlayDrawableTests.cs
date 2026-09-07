using MAUIDesigner.Fresh.App.Rendering;
using Microsoft.Maui.Graphics;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class GridTrackOverlayDrawableTests
{
    [Fact]
    public void Update_precomputes_track_boundaries_from_actual_definitions()
    {
        var drawable = new GridTrackOverlayDrawable();

        drawable.UpdateActualTracks(
            [40, 60, 124],
            [100, 196],
            new RectF(10, 20, 300, 240),
            rowSpacing: 8,
            columnSpacing: 4,
            deviceZoom: 2);

        Assert.Equal(3, drawable.LineCount);
        Assert.Equal(new GridTrackLine(10, 64, 310, 64), drawable.GetLine(0));
        Assert.Equal(new GridTrackLine(10, 132, 310, 132), drawable.GetLine(1));
        Assert.Equal(new GridTrackLine(112, 20, 112, 260), drawable.GetLine(2));
    }

    [Fact]
    public void Update_reuses_capacity_and_clear_removes_all_lines()
    {
        var drawable = new GridTrackOverlayDrawable();
        drawable.UpdateActualTracks(
            [50, 50],
            [],
            new RectF(0, 0, 100, 100),
            0,
            0,
            1);
        Assert.Single(Enumerable.Range(0, drawable.LineCount));

        drawable.Clear();

        Assert.Equal(0, drawable.LineCount);
    }
}
