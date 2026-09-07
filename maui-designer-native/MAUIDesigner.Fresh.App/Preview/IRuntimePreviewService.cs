using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Preview;

public interface IRuntimePreviewService
{
    Task<Window> OpenAsync(
        DesignerDocument document,
        RuntimePreviewOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed record RuntimePreviewOptions(
    string DeviceName,
    double Width,
    double Height)
{
    public static RuntimePreviewOptions Responsive { get; } =
        new("Responsive", 0, 0);

    public string DisplayText =>
        Width > 0 && Height > 0
            ? $"{DeviceName} · {Width:0} × {Height:0}"
            : DeviceName;
}
