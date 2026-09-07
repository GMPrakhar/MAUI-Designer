using MAUIDesigner.Fresh.App.Catalog;

namespace MAUIDesigner.Fresh.App.Rendering;

public static class DesignerInputPolicy
{
    public static bool SuppressRuntimeInput(ControlDescriptor descriptor) =>
        !descriptor.AcceptsChildren;
}
