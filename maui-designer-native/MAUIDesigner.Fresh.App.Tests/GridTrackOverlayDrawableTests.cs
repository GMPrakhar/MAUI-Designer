using MAUIDesigner.Fresh.App.Rendering;
using Microsoft.Maui.Graphics;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class GridTrackOverlayDrawableTests
{
    [Fact]
    public void Rulers_follow_the_transformed_design_surface()
    {
        var viewport = new MAUIDesigner.Fresh.App.Viewport.DesignerViewportState();
        viewport.ZoomAt(0.5, 0, 0);
        viewport.PanBy(80, 60);

        RectF design = CanvasRulerDrawable.GetDesignBounds(viewport);
        RectF horizontal = CanvasRulerDrawable.GetHorizontalRulerBounds(
            design,
            new RectF(0, 0, 800, 600));
        RectF vertical = CanvasRulerDrawable.GetVerticalRulerBounds(
            design,
            new RectF(0, 0, 800, 600));

        Assert.Equal(new RectF(80, 38, 512, 22), horizontal);
        Assert.Equal(new RectF(58, 60, 22, 360), vertical);
    }

    [Fact]
    public void Horizontal_ruler_remains_visible_when_vertical_edge_is_panned_offscreen()
    {
        var design = new RectF(-100, 60, 512, 360);
        var viewport = new RectF(0, 0, 800, 600);

        RectF horizontal = CanvasRulerDrawable.GetHorizontalRulerBounds(design, viewport);
        RectF vertical = CanvasRulerDrawable.GetVerticalRulerBounds(design, viewport);

        Assert.Equal(new RectF(0, 38, 412, 22), horizontal);
        Assert.Equal(0, vertical.Width);
    }

    [Theory]
    [InlineData(0.25, 500)]
    [InlineData(0.5, 200)]
    [InlineData(1, 100)]
    [InlineData(2, 50)]
    [InlineData(3, 50)]
    public void Ruler_major_ticks_remain_legible_at_every_zoom(
        double zoom,
        double expectedStep)
    {
        Assert.Equal(expectedStep, CanvasRulerDrawable.GetMajorTickStep(zoom));
    }

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
