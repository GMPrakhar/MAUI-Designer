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
            ImmutableDictionary<string, DesignerValue>.Empty
                .Add("Grid.Row", DesignerValue.Literal("0"))
                .Add("AbsoluteLayout.LayoutBounds", DesignerValue.Literal("1,2,30,40"))
                .Add("AbsoluteLayout.LayoutFlags", DesignerValue.Literal("None")),
            bounds: new RectD(1, 2, 30, 40),
            parentPropertyName: "Header")));

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
        Assert.False(placed.Properties.ContainsKey("AbsoluteLayout.LayoutBounds"));
        Assert.False(placed.Properties.ContainsKey("AbsoluteLayout.LayoutFlags"));
        Assert.Null(placed.ParentPropertyName);

        Assert.True(session.Undo());
        DesignerNode restored = Assert.IsType<DesignerNode>(session.Current.Find(sourceId)!.Find(labelId));
        Assert.Equal(new RectD(1, 2, 30, 40), restored.Bounds);
        Assert.Equal("0", restored.Properties["Grid.Row"].Text);
        Assert.False(restored.Properties.ContainsKey("Grid.Column"));
        Assert.Equal("1,2,30,40", restored.Properties["AbsoluteLayout.LayoutBounds"].Text);
        Assert.Equal("None", restored.Properties["AbsoluteLayout.LayoutFlags"].Text);
        Assert.Equal("Header", restored.ParentPropertyName);
    }

    [Fact]
    public void Clone_subtree_regenerates_all_ids_and_preserves_content()
    {
        var child = new DesignerNode(
            new ElementId("child"),
            LabelType,
            ImmutableDictionary<string, DesignerValue>.Empty
                .Add("Text", DesignerValue.Literal("Preserved")),
            bounds: new RectD(1, 2, 30, 40),
            preservedContent: [new XamlSyntaxFragment("<Label.GestureRecognizers />")],
            parentPropertyName: "Header");
        var source = new DesignerNode(
            new ElementId("source"),
            GridType,
            ImmutableDictionary<string, DesignerValue>.Empty
                .Add("Padding", DesignerValue.Literal("8")),
            [child],
            new RectD(10, 20, 200, 100));

        DesignerNode first = DesignerNodeCloner.CloneSubtree(source);
        DesignerNode second = DesignerNodeCloner.CloneSubtree(source);

        Assert.NotEqual(source.Id, first.Id);
        Assert.NotEqual(child.Id, first.Children[0].Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Children[0].Id, second.Children[0].Id);
        Assert.Equal(source.ControlType, first.ControlType);
        Assert.Equal(source.Properties, first.Properties);
        Assert.Equal(source.Bounds, first.Bounds);
        Assert.Equal(child.Properties, first.Children[0].Properties);
        Assert.Equal(child.Bounds, first.Children[0].Bounds);
        Assert.Equal(child.PreservedContent, first.Children[0].PreservedContent);
        Assert.Equal("Header", first.Children[0].ParentPropertyName);
        Assert.Equal(new ElementId("source"), source.Id);
        Assert.Equal(new ElementId("child"), source.Children[0].Id);
    }

    [Fact]
    public void Clone_subtree_rejects_duplicate_ids_from_factory()
    {
        var source = new DesignerNode(
            new ElementId("source"),
            GridType,
            children: [new DesignerNode(new ElementId("child"), LabelType)]);

        Assert.Throws<InvalidOperationException>(() =>
            DesignerNodeCloner.CloneSubtree(source, () => new ElementId("duplicate")));
    }

    [Fact]
    public void Clone_subtree_regenerates_names_and_internal_references()
    {
        var source = new DesignerNode(
            new ElementId("source"),
            GridType,
            ImmutableDictionary<string, DesignerValue>.Empty
                .Add("x:Name", DesignerValue.Literal("Card"))
                .Add(
                    "IsVisible",
                    new DesignerValue(
                        "{Binding IsReady, Source={x:Reference Title}}",
                        DesignerValueKind.MarkupExtension))
                .Add(
                    "BindingPath",
                    new DesignerValue(
                        "{Binding Title}",
                        DesignerValueKind.MarkupExtension))
                .Add(
                    "Resource",
                    new DesignerValue(
                        "{StaticResource Title}",
                        DesignerValueKind.MarkupExtension))
                .Add(
                    "ElementBinding",
                    new DesignerValue(
                        "{Binding Text, ElementName=Title}",
                        DesignerValueKind.MarkupExtension)),
            [
                new DesignerNode(
                    new ElementId("child"),
                    LabelType,
                    ImmutableDictionary<string, DesignerValue>.Empty
                        .Add("x:Name", DesignerValue.Literal("Title"))
                        .Add("Text", DesignerValue.Literal("Title")),
                    preservedContent:
                    [
                        new XamlSyntaxFragment(
                            "<Reference Target=\"{x:Reference Card}\" />"),
                        new XamlSyntaxFragment(
                            "<Setter Value=\"{Binding Source={x:Reference Name=&quot;Title&quot;}}\" />"),
                        new XamlSyntaxFragment(
                            "<x:String x:Key=\"Hint\">ElementName=Title}</x:String>")
                    ])
            ]);
        var ids = new Queue<ElementId>(
            [new ElementId("clone-root"), new ElementId("clone-title")]);

        DesignerNode clone = DesignerNodeCloner.CloneSubtree(source, ids.Dequeue);

        Assert.Equal("Card_Copy_clone_root", clone.Properties["x:Name"].Text);
        Assert.Equal(
            "{Binding IsReady, Source={x:Reference Title_Copy_clone_title}}",
            clone.Properties["IsVisible"].Text);
        Assert.Equal("{Binding Title}", clone.Properties["BindingPath"].Text);
        Assert.Equal("{StaticResource Title}", clone.Properties["Resource"].Text);
        Assert.Equal(
            "{Binding Text, ElementName=Title_Copy_clone_title}",
            clone.Properties["ElementBinding"].Text);
        Assert.Equal("Title_Copy_clone_title", clone.Children[0].Properties["x:Name"].Text);
        Assert.Equal("Title", clone.Children[0].Properties["Text"].Text);
        Assert.Contains(
            "Card_Copy_clone_root",
            clone.Children[0].PreservedContent[0].Xml);
        Assert.Contains(
            "Title_Copy_clone_title",
            clone.Children[0].PreservedContent[1].Xml);
        Assert.Equal(
            "<x:String x:Key=\"Hint\">ElementName=Title}</x:String>",
            clone.Children[0].PreservedContent[2].Xml);
    }

    [Fact]
    public void Reorder_preserves_node_data_and_undoes_atomically()
    {
        var session = new DocumentSession(DesignerDocument.Create(GridType));
        var firstId = new ElementId("first");
        var secondId = new ElementId("second");
        var second = new DesignerNode(
            secondId,
            LabelType,
            ImmutableDictionary<string, DesignerValue>.Empty
                .Add("Text", DesignerValue.Literal("Second")),
            bounds: new RectD(4, 8, 120, 32),
            parentPropertyName: "Header");
        session.Execute(new AddElementCommand(
            new ElementId("root"),
            new DesignerNode(firstId, LabelType)));
        session.Execute(new AddElementCommand(new ElementId("root"), second));

        session.Execute(new ReorderElementCommand(secondId, 0));

        DesignerNode reordered = session.Current.Root.Children[0];
        Assert.Same(second, reordered);
        Assert.Equal("Header", reordered.ParentPropertyName);
        Assert.Equal(new RectD(4, 8, 120, 32), reordered.Bounds);
        Assert.True(session.Undo());
        Assert.Equal(
            new[] { firstId, secondId },
            session.Current.Root.Children.Select(child => child.Id));
    }
}
