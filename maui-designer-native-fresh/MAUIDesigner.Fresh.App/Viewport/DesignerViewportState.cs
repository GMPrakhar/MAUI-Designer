namespace MAUIDesigner.Fresh.App.Viewport;

public sealed class DesignerViewportState
{
    public const double MinimumZoom = 0.25;
    public const double MaximumZoom = 3;

    public IReadOnlyList<DevicePreset> Devices { get; } =
    [
        new("phone", "Phone 390 x 844", 390, 844),
        new("phone-small", "Small phone 360 x 640", 360, 640),
        new("tablet", "Tablet 768 x 1024", 768, 1024),
        new("desktop", "Desktop 1280 x 800", 1280, 800),
        new("custom", "Desktop 1024 x 720", 1024, 720)
    ];

    public double Zoom { get; private set; } = 1;

    public double PanX { get; private set; }

    public double PanY { get; private set; }

    public double DesignWidth { get; private set; } = 1024;

    public double DesignHeight { get; private set; } = 720;

    public int GridSize { get; private set; } = 8;

    public bool ShowGrid { get; private set; }

    public bool ShowRulers { get; private set; } = true;

    public bool SnapToGrid { get; private set; } = true;

    public bool IsDarkPreview { get; private set; }

    public DevicePreset SelectedDevice { get; private set; }

    public DesignerViewportState()
    {
        SelectedDevice = Devices[^1];
    }

    public void SelectDevice(DevicePreset device)
    {
        ArgumentNullException.ThrowIfNull(device);
        SelectedDevice = device;
        DesignWidth = device.Width;
        DesignHeight = device.Height;
    }

    public void PanBy(double deltaX, double deltaY)
    {
        PanX += deltaX;
        PanY += deltaY;
    }

    public void ZoomAt(double requestedZoom, double focalX, double focalY)
    {
        double nextZoom = ClampZoom(requestedZoom);
        double designX = (focalX - PanX) / Zoom;
        double designY = (focalY - PanY) / Zoom;
        PanX = focalX - designX * nextZoom;
        PanY = focalY - designY * nextZoom;
        Zoom = nextZoom;
    }

    public void Fit(double viewportWidth, double viewportHeight, double margin = 64)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        double availableWidth = Math.Max(1, viewportWidth - margin);
        double availableHeight = Math.Max(1, viewportHeight - margin);
        Zoom = ClampZoom(Math.Min(
            availableWidth / DesignWidth,
            availableHeight / DesignHeight));
        Center(viewportWidth, viewportHeight);
    }

    public void Reset(double viewportWidth, double viewportHeight)
    {
        Zoom = 1;
        Center(viewportWidth, viewportHeight);
    }

    public double ToDesignDelta(double viewportDelta) => viewportDelta / Zoom;

    public double Snap(double value) =>
        SnapToGrid
            ? Math.Round(value / GridSize, MidpointRounding.AwayFromZero) * GridSize
            : Math.Round(value);

    public void SetGridSize(int gridSize) =>
        GridSize = Math.Clamp(gridSize, 2, 200);

    public void ToggleGrid() => ShowGrid = !ShowGrid;

    public void ToggleRulers() => ShowRulers = !ShowRulers;

    public void ToggleSnap() => SnapToGrid = !SnapToGrid;

    public void ToggleTheme() => IsDarkPreview = !IsDarkPreview;

    private static double ClampZoom(double zoom) =>
        Math.Round(Math.Clamp(zoom, MinimumZoom, MaximumZoom), 2);

    private void Center(double viewportWidth, double viewportHeight)
    {
        PanX = (viewportWidth - DesignWidth * Zoom) / 2;
        PanY = (viewportHeight - DesignHeight * Zoom) / 2;
    }
}

public sealed record DevicePreset(
    string Id,
    string Name,
    double Width,
    double Height);
