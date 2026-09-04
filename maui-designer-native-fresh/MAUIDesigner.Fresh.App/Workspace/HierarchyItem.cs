using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Workspace;

public sealed record HierarchyItem(
    ElementId ElementId,
    string DisplayName,
    string Identity,
    Thickness Indent,
    bool IsSelected);
