using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;
using Microsoft.Extensions.DependencyInjection;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class DesignerWorkspaceTests
{
    [Fact]
    public void Insertion_skips_full_single_content_ancestors()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor contentView = Find(catalog, typeof(ContentView));
        ControlDescriptor label = Find(catalog, typeof(Label));
        ControlDescriptor button = Find(catalog, typeof(Button));

        ElementId contentId = workspace.Add(contentView);
        ElementId labelId = workspace.Add(label);
        ElementId buttonId = workspace.Add(button);

        DesignerNode root = workspace.Session.Current.Root;
        DesignerNode content = Assert.IsType<DesignerNode>(root.Find(contentId));
        Assert.Equal(labelId, Assert.Single(content.Children).Id);
        Assert.Contains(root.Children, child => child.Id == buttonId);
        Assert.Throws<InvalidOperationException>(() =>
            workspace.Add(button, contentId));
    }

    [Fact]
    public void Drop_target_rejects_the_moving_subtree()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor grid = Find(catalog, typeof(Grid));
        ElementId outerId = workspace.Add(grid);
        ElementId innerId = workspace.Add(grid);

        Assert.False(workspace.CanAcceptChild(outerId, outerId));
        Assert.False(workspace.CanAcceptChild(innerId, outerId));
    }

    [Fact]
    public void Copy_and_paste_clone_subtrees_with_unique_ids_and_cascading_absolute_offsets()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor stack = Find(catalog, typeof(VerticalStackLayout));
        ControlDescriptor label = Find(catalog, typeof(Label));
        ElementId stackId = workspace.Add(stack);
        ElementId labelId = workspace.Add(label);
        workspace.Session.Execute(new SetPropertyCommand(
            labelId,
            nameof(Label.Text),
            DesignerValue.Literal("Copied child")));
        DesignerNode source = workspace.Session.Current.Find(stackId)!;
        RectD sourceBounds = Assert.IsType<RectD>(source.Bounds);

        workspace.Select(stackId);
        workspace.CopySelection();
        workspace.Select(workspace.Session.Current.Root.Id);
        ElementId firstId = Assert.IsType<ElementId>(workspace.Paste());
        workspace.Select(workspace.Session.Current.Root.Id);
        ElementId secondId = Assert.IsType<ElementId>(workspace.Paste());

        DesignerNode first = workspace.Session.Current.Find(firstId)!;
        DesignerNode second = workspace.Session.Current.Find(secondId)!;
        Assert.Equal(sourceBounds.X + 16, first.Bounds!.Value.X);
        Assert.Equal(sourceBounds.Y + 16, first.Bounds.Value.Y);
        Assert.Equal(sourceBounds.X + 32, second.Bounds!.Value.X);
        Assert.Equal(sourceBounds.Y + 32, second.Bounds.Value.Y);
        Assert.Equal("Copied child", Assert.Single(first.Children).Properties[nameof(Label.Text)].Text);
        Assert.NotEqual(labelId, first.Children[0].Id);
        Assert.NotEqual(first.Children[0].Id, second.Children[0].Id);
        Assert.Equal(
            workspace.Session.Current.Root
                .SelectMany()
                .Select(node => node.Id)
                .Distinct()
                .Count(),
            workspace.Session.Current.Root.SelectMany().Count());
    }

    [Fact]
    public void Paste_uses_the_nearest_valid_selected_container()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor stack = Find(catalog, typeof(VerticalStackLayout));
        ControlDescriptor label = Find(catalog, typeof(Label));
        ControlDescriptor button = Find(catalog, typeof(Button));
        ElementId stackId = workspace.Add(stack);
        ElementId labelId = workspace.Add(label);
        ElementId buttonId = workspace.Add(button, workspace.Session.Current.Root.Id);
        workspace.Select(buttonId);
        workspace.CopySelection();

        workspace.Select(labelId);
        Assert.True(workspace.CanPaste);
        ElementId pastedId = Assert.IsType<ElementId>(workspace.Paste());

        Assert.NotNull(workspace.Session.Current.Find(stackId)!.Find(pastedId));
        Assert.Null(workspace.Session.Current.Find(pastedId)!.Bounds);
    }

    [Fact]
    public void Cut_keeps_an_internal_snapshot_and_undoes()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor label = Find(catalog, typeof(Label));
        ElementId labelId = workspace.Add(label);
        workspace.Session.Execute(new SetPropertyCommand(
            labelId,
            nameof(Label.Text),
            DesignerValue.Literal("Cut me")));

        workspace.CutSelection();

        Assert.Null(workspace.Session.Current.Find(labelId));
        Assert.Equal(workspace.Session.Current.Root.Id, workspace.SelectedId);
        Assert.True(workspace.CanPaste);
        Assert.True(workspace.Session.Undo());
        Assert.NotNull(workspace.Session.Current.Find(labelId));
    }

    [Fact]
    public void Deleting_the_root_resets_the_document_and_undo_restores_it()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor label = Find(catalog, typeof(Label));
        ElementId labelId = workspace.Add(label);
        workspace.Select(workspace.Session.Current.Root.Id);
        workspace.DeleteSelection();

        Assert.Empty(workspace.Session.Current.Root.Children);
        Assert.Equal(typeof(AbsoluteLayout).Name, workspace.Session.Current.Root.ControlType.XamlName);
        Assert.True(workspace.Session.Undo());
        Assert.NotNull(workspace.Session.Current.Find(labelId));
    }

    [Fact]
    public void Multi_selection_deletes_as_one_undoable_change()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor button = Find(catalog, typeof(Button));
        ElementId firstId = workspace.Add(button);
        ElementId secondId = workspace.Add(button);
        workspace.SetSelection([firstId, secondId], additive: false);

        workspace.DeleteSelection();

        Assert.Null(workspace.Session.Current.Find(firstId));
        Assert.Null(workspace.Session.Current.Find(secondId));
        Assert.True(workspace.Session.Undo());
        Assert.NotNull(workspace.Session.Current.Find(firstId));
        Assert.NotNull(workspace.Session.Current.Find(secondId));
    }

    [Fact]
    public void Multi_selection_copy_paste_and_duplicate_preserve_the_group()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor button = Find(catalog, typeof(Button));
        ElementId firstId = workspace.Add(button);
        ElementId secondId = workspace.Add(button);
        RectD firstBounds = workspace.Session.Current.Find(firstId)!.Bounds!.Value;
        RectD secondBounds = workspace.Session.Current.Find(secondId)!.Bounds!.Value;
        workspace.SetSelection([firstId, secondId], additive: false);
        workspace.CopySelection();
        workspace.Select(workspace.Session.Current.Root.Id);

        Assert.NotNull(workspace.Paste());
        Assert.Equal(2, workspace.SelectionCount);
        Assert.Equal(4, workspace.Session.Current.Root.Children.Length);
        DesignerNode[] pasted = workspace.Session.Current.Root.Children.Skip(2).ToArray();
        Assert.Equal(firstBounds.X + 16, pasted[0].Bounds!.Value.X);
        Assert.Equal(firstBounds.Y + 16, pasted[0].Bounds!.Value.Y);
        Assert.Equal(secondBounds.X + 16, pasted[1].Bounds!.Value.X);
        Assert.Equal(secondBounds.Y + 16, pasted[1].Bounds!.Value.Y);

        Assert.NotNull(workspace.DuplicateSelection());
        Assert.Equal(2, workspace.SelectionCount);
        Assert.Equal(6, workspace.Session.Current.Root.Children.Length);
    }

    [Fact]
    public void Toggle_selection_supports_control_click_semantics()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor button = Find(catalog, typeof(Button));
        ElementId firstId = workspace.Add(button);
        ElementId secondId = workspace.Add(button);

        workspace.Select(firstId);
        workspace.ToggleSelection(secondId);

        Assert.Equal(2, workspace.SelectionCount);
        Assert.Contains(firstId, workspace.SelectedIds);
        Assert.Contains(secondId, workspace.SelectedIds);
        workspace.ToggleSelection(secondId);
        Assert.Equal([firstId], workspace.SelectedIds);
    }

    [Fact]
    public void Duplicate_is_adjacent_offset_and_undoes_in_one_step()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor button = Find(catalog, typeof(Button));
        ElementId originalId = workspace.Add(button);
        DesignerNode original = workspace.Session.Current.Find(originalId)!;

        ElementId duplicateId = Assert.IsType<ElementId>(workspace.DuplicateSelection());

        Assert.Equal(
            new[] { originalId, duplicateId },
            workspace.Session.Current.Root.Children.Select(child => child.Id));
        DesignerNode duplicate = workspace.Session.Current.Find(duplicateId)!;
        Assert.Equal(original.Bounds!.Value.X + 16, duplicate.Bounds!.Value.X);
        Assert.Equal(original.Bounds.Value.Y + 16, duplicate.Bounds.Value.Y);
        Assert.True(workspace.Session.Undo());
        Assert.Null(workspace.Session.Current.Find(duplicateId));
        Assert.NotNull(workspace.Session.Current.Find(originalId));
    }

    [Fact]
    public void Hierarchy_reorder_moves_among_siblings_and_preserves_placement()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var workspace = new DesignerWorkspace(catalog);
        ControlDescriptor button = Find(catalog, typeof(Button));
        ElementId firstId = workspace.Add(button);
        ElementId secondId = workspace.Add(button);
        ElementId thirdId = workspace.Add(button);
        workspace.Select(secondId);
        DesignerNode before = workspace.Session.Current.Find(secondId)!;

        Assert.True(workspace.CanMoveSelectionUp);
        Assert.True(workspace.CanMoveSelectionDown);
        Assert.True(workspace.MoveSelectionUp());
        Assert.Equal(
            new[] { secondId, firstId, thirdId },
            workspace.Session.Current.Root.Children.Select(child => child.Id));
        Assert.Same(before, workspace.Session.Current.Find(secondId));
        Assert.True(workspace.Session.Undo());
        Assert.Equal(
            new[] { firstId, secondId, thirdId },
            workspace.Session.Current.Root.Children.Select(child => child.Id));

        Assert.True(workspace.MoveSelectionDown());
        Assert.Equal(
            new[] { firstId, thirdId, secondId },
            workspace.Session.Current.Root.Children.Select(child => child.Id));
        Assert.False(workspace.CanMoveSelectionDown);
    }

    private static ReflectionControlCatalog CreateCatalog()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);
        return catalog;
    }

    private static ControlDescriptor Find(ReflectionControlCatalog catalog, Type type) =>
        catalog.Controls.Single(control => control.RuntimeType == type);
}

internal static class DesignerNodeTestExtensions
{
    public static IEnumerable<DesignerNode> SelectMany(this DesignerNode node)
    {
        yield return node;
        foreach (DesignerNode child in node.Children)
        {
            foreach (DesignerNode descendant in child.SelectMany())
            {
                yield return descendant;
            }
        }
    }
}
