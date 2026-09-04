using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;
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
