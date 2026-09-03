using System.Collections.Immutable;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.Core.Tests;

public sealed class DocumentSessionTests
{
    private static readonly ControlTypeId GridType =
        new("Microsoft.Maui.Controls", "Microsoft.Maui.Controls.Grid", MauiNamespace, "Grid");

    private static readonly ControlTypeId LabelType =
        new("Microsoft.Maui.Controls", "Microsoft.Maui.Controls.Label", MauiNamespace, "Label");

    private const string MauiNamespace = "http://schemas.microsoft.com/dotnet/2021/maui";

    [Fact]
    public void Commands_add_update_remove_and_undo_as_one_authoritative_path()
    {
        var session = new DocumentSession(DesignerDocument.Create(GridType));
        var labelId = new ElementId("label-1");

        session.Execute(new AddElementCommand(
            new ElementId("root"),
            new DesignerNode(labelId, LabelType)));
        session.Execute(new SetPropertyCommand(labelId, "Text", DesignerValue.Literal("Hello")));
        session.Execute(new SetBoundsCommand(labelId, new RectD(12, 16, 120, 32)));

        DesignerNode label = Assert.IsType<DesignerNode>(session.Current.Find(labelId));
        Assert.Equal("Hello", label.Properties["Text"].Text);
        Assert.Equal(new RectD(12, 16, 120, 32), label.Bounds);

        Assert.True(session.Undo());
        Assert.Null(session.Current.Find(labelId)!.Bounds);
        Assert.True(session.Redo());
        Assert.Equal(new RectD(12, 16, 120, 32), session.Current.Find(labelId)!.Bounds);

        session.Execute(new RemoveElementCommand(labelId));
        Assert.Null(session.Current.Find(labelId));
    }

    [Fact]
    public void Reparent_preserves_subtree_and_prevents_cycles()
    {
        var session = new DocumentSession(DesignerDocument.Create(GridType));
        var outerId = new ElementId("outer");
        var innerId = new ElementId("inner");
        var labelId = new ElementId("label");
        session.Execute(new AddElementCommand(new ElementId("root"), new DesignerNode(outerId, GridType)));
        session.Execute(new AddElementCommand(outerId, new DesignerNode(innerId, GridType)));
        session.Execute(new AddElementCommand(outerId, new DesignerNode(labelId, LabelType)));

        session.Execute(new ReparentElementCommand(labelId, innerId, Bounds: new RectD(1, 2, 30, 40)));

        DesignerNode moved = Assert.IsType<DesignerNode>(session.Current.Find(innerId)!.Find(labelId));
        Assert.Equal(new RectD(1, 2, 30, 40), moved.Bounds);
        Assert.Throws<InvalidOperationException>(() =>
            session.Execute(new ReparentElementCommand(outerId, innerId)));
    }

    [Fact]
    public void Same_parent_reorder_uses_post_removal_index()
    {
        var session = new DocumentSession(DesignerDocument.Create(GridType));
        foreach (string id in new[] { "a", "b", "c" })
        {
            session.Execute(new AddElementCommand(
                new ElementId("root"),
                new DesignerNode(new ElementId(id), LabelType)));
        }

        session.Execute(new ReparentElementCommand(new ElementId("c"), new ElementId("root"), 0));

        Assert.Equal(
            new[] { "c", "a", "b" },
            session.Current.Root.Children.Select(child => child.Id.Value));
    }

    [Fact]
    public void Duplicate_ids_are_rejected_before_document_changes()
    {
        var session = new DocumentSession(DesignerDocument.Create(GridType));
        var id = new ElementId("same");
        session.Execute(new AddElementCommand(new ElementId("root"), new DesignerNode(id, LabelType)));
        DesignerDocument before = session.Current;

        Assert.Throws<InvalidOperationException>(() =>
            session.Execute(new AddElementCommand(new ElementId("root"), new DesignerNode(id, LabelType))));
        Assert.Same(before, session.Current);
    }

    [Fact]
    public void Placement_updates_parent_bounds_and_attached_properties_atomically()
    {
        var session = new DocumentSession(DesignerDocument.Create(GridType));
        var sourceId = new ElementId("source");
        var destinationId = new ElementId("destination");
        var labelId = new ElementId("label");
        session.Execute(new AddElementCommand(new ElementId("root"), new DesignerNode(sourceId, GridType)));
        session.Execute(new AddElementCommand(new ElementId("root"), new DesignerNode(destinationId, GridType)));
        session.Execute(new AddElementCommand(sourceId, new DesignerNode(
            labelId,
            LabelType,
            ImmutableDictionary<string, DesignerValue>.Empty.Add("Grid.Row", DesignerValue.Literal("0")))));

        session.Execute(new PlaceElementCommand(
            labelId,
            destinationId,
            Bounds: new RectD(4, 8, 100, 32),
            PropertyUpdates: ImmutableDictionary<string, DesignerValue?>.Empty
                .Add("Grid.Row", DesignerValue.Literal("2"))
                .Add("Grid.Column", DesignerValue.Literal("1"))));

        DesignerNode placed = Assert.IsType<DesignerNode>(session.Current.Find(destinationId)!.Find(labelId));
        Assert.Equal(new RectD(4, 8, 100, 32), placed.Bounds);
        Assert.Equal("2", placed.Properties["Grid.Row"].Text);
        Assert.Equal("1", placed.Properties["Grid.Column"].Text);

        Assert.True(session.Undo());
        DesignerNode restored = Assert.IsType<DesignerNode>(session.Current.Find(sourceId)!.Find(labelId));
        Assert.Null(restored.Bounds);
        Assert.Equal("0", restored.Properties["Grid.Row"].Text);
        Assert.False(restored.Properties.ContainsKey("Grid.Column"));
    }
}
