using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Rendering;

public static class DesignerControlSafetyPolicy
{
    public static bool TryGetPlaceholderReason(
        ControlTypeId controlType,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(controlType);

        if (controlType.AssemblyName == "Syncfusion.Maui.Core" &&
            controlType.FullName == "Syncfusion.Maui.Core.SfGlassEffectView")
        {
            reason =
                "Toolbox insertion and live preview are disabled because this " +
                "control terminates the embedded Windows designer. Existing XAML " +
                "and properties are preserved.";
            return true;
        }

        if (controlType.AssemblyName == "Syncfusion.Maui.Core" &&
            controlType.FullName == "Syncfusion.Maui.Core.SfAvatarView")
        {
            reason =
                "Toolbox insertion and live preview are disabled because this " +
                "control terminates the embedded Windows designer. Existing XAML " +
                "and properties are preserved.";
            return true;
        }

        reason = null;
        return false;
    }
}
