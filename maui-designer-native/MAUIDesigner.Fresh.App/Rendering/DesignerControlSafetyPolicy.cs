using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Rendering;

public static class DesignerControlSafetyPolicy
{
    private const string FrameworkAssemblyName = "Microsoft.Maui.Controls";
    private const string FrameworkTypeNamespace = "Microsoft.Maui.Controls.";

    public static bool TryGetPlaceholderReason(
        ControlTypeId controlType,
        out string? reason,
        bool hasProjectResources = false)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (hasProjectResources)
        {
            reason = null;
            return false;
        }

        if (!string.Equals(
                controlType.AssemblyName,
                FrameworkAssemblyName,
                StringComparison.Ordinal) ||
            !controlType.FullName.StartsWith(
                FrameworkTypeNamespace,
                StringComparison.Ordinal))
        {
            reason =
                "Live preview uses a design-time placeholder because the host " +
                "does not include this project's package resources. XAML, " +
                "hierarchy, and properties are preserved.";
            return true;
        }

        reason = null;
        return false;
    }
}
