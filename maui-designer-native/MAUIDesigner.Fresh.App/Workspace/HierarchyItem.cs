using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Workspace;

public sealed record HierarchyItem(
    DesignerNode Node,
    int Depth,
    bool IsExpanded);

public static class HierarchyProjection
{
    public static IReadOnlyList<HierarchyItem> Build(
        DesignerNode root,
        IReadOnlySet<ElementId> collapsed)
    {
        var items = new List<HierarchyItem>();
        Add(root, 0, collapsed, items);
        return items;
    }

    private static void Add(
        DesignerNode node,
        int depth,
        IReadOnlySet<ElementId> collapsed,
        List<HierarchyItem> items)
    {
        bool expanded = node.Children.Length > 0 && !collapsed.Contains(node.Id);
        items.Add(new HierarchyItem(node, depth, expanded));
        if (!expanded)
        {
            return;
        }

        foreach (DesignerNode child in node.Children)
        {
            Add(child, depth + 1, collapsed, items);
        }
    }
}
