using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Rendering;

public static class SelectionChromePresentation
{
    public static string LabelFor(
        DesignerNode node,
        ControlDescriptor? descriptor) =>
        descriptor?.DisplayName ?? node.ControlType.XamlName;
}
