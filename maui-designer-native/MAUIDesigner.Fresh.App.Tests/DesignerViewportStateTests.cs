using MAUIDesigner.Fresh.App.Viewport;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class DesignerViewportStateTests
{
    [Fact]
    public void Zoom_at_keeps_the_design_point_under_the_pointer()
    {
        var viewport = new DesignerViewportState();
        viewport.PanBy(40, 20);
        double designX = (300 - viewport.PanX) / viewport.Zoom;
        double designY = (200 - viewport.PanY) / viewport.Zoom;

        viewport.ZoomAt(2, 300, 200);

        Assert.Equal(300, viewport.PanX + designX * viewport.Zoom, 6);
        Assert.Equal(200, viewport.PanY + designY * viewport.Zoom, 6);
    }

    [Fact]
    public void Fit_scales_and_centers_the_selected_device()
    {
        var viewport = new DesignerViewportState();
        viewport.SelectDevice(new DevicePreset("test", "Test", 400, 200));

        viewport.Fit(1000, 800);

        Assert.Equal(2.34, viewport.Zoom);
        Assert.Equal(32, viewport.PanX, 6);
        Assert.Equal(166, viewport.PanY, 6);
    }

    [Fact]
    public void Zoom_and_grid_snapping_are_bounded_and_deterministic()
    {
        var viewport = new DesignerViewportState();

        viewport.ZoomAt(100, 0, 0);
        Assert.Equal(DesignerViewportState.MaximumZoom, viewport.Zoom);
        viewport.ZoomAt(0.01, 0, 0);
        Assert.Equal(DesignerViewportState.MinimumZoom, viewport.Zoom);
        Assert.Equal(16, viewport.Snap(13));
        Assert.Equal(8, viewport.Snap(11));
        viewport.ToggleSnap();
        Assert.Equal(13, viewport.Snap(13.4));
        viewport.SetGridSize(500);
        Assert.Equal(200, viewport.GridSize);
    }
}
