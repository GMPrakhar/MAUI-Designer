using System.Collections.Immutable;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class HierarchyProjectionTests
{
    [Fact]
    public void Collapsed_branches_hide_only_their_descendants()
    {
        DesignerNode child = Node("child", Node("grandchild"));
        DesignerNode sibling = Node("sibling");
        DesignerNode root = Node("root", child, sibling);

        IReadOnlyList<HierarchyItem> rows = HierarchyProjection.Build(
            root,
            new HashSet<ElementId> { child.Id });

        Assert.Equal(["root", "child", "sibling"], rows.Select(row => row.Node.Id.Value));
        Assert.True(rows[0].IsExpanded);
        Assert.False(rows[1].IsExpanded);
        Assert.Equal(1, rows[1].Depth);
    }

    private static DesignerNode Node(string id, params DesignerNode[] children) =>
        new(
            new ElementId(id),
            new ControlTypeId("tests", id, "urn:tests", id),
            ImmutableDictionary<string, DesignerValue>.Empty,
            children.ToImmutableArray());
}
