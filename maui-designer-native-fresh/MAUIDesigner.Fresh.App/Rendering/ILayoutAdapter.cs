using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Rendering;

public interface ILayoutAdapter
{
    bool CanHandle(ControlDescriptor descriptor);

    void AddChild(View parent, View child, DesignerNode childNode);

    LayoutPlacement ResolveDrop(View parent, DesignerNode parentNode, PointD position);

    void AddDropPreview(View parent, LayoutPlacement placement);
}
