using MAUIDesigner.Fresh.Core.Documents;
namespace MAUIDesigner.Fresh.App.Rendering;

public static class DesignerControlSafetyPolicy
{
    private const string FrameworkXamlNamespace =
        "http://schemas.microsoft.com/dotnet/2021/maui";

    public static bool TryGetPlaceholderReason(
        ControlTypeId controlType,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (!string.Equals(
                controlType.XamlNamespace,
                FrameworkXamlNamespace,
                StringComparison.Ordinal))
        {
            reason =
                "Live preview uses a design-time placeholder because dynamically " +
                "loaded controls can terminate the Windows designer. XAML, " +
                "hierarchy, and properties are preserved.";
            return true;
        }

        reason = null;
        return false;
    }
}
