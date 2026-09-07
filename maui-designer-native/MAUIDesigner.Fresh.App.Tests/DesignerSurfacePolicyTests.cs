using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Controls;
using MAUIDesigner.Fresh.App.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class DesignerSurfacePolicyTests
{
    [Fact]
    public void Runtime_input_is_suppressed_for_leaf_controls_but_not_containers()
    {
        ReflectionControlCatalog catalog = CreateCatalog();

        Assert.True(DesignerInputPolicy.SuppressRuntimeInput(Find(catalog, typeof(Button))));
        Assert.True(DesignerInputPolicy.SuppressRuntimeInput(Find(catalog, typeof(CheckBox))));
        Assert.False(DesignerInputPolicy.SuppressRuntimeInput(Find(catalog, typeof(Grid))));
        Assert.False(DesignerInputPolicy.SuppressRuntimeInput(Find(catalog, typeof(ContentView))));
    }

    [Theory]
    [InlineData("Label", ControlIconKind.Text)]
    [InlineData("Button", ControlIconKind.Button)]
    [InlineData("Entry", ControlIconKind.TextInput)]
    [InlineData("Grid", ControlIconKind.Grid)]
    [InlineData("VerticalStackLayout", ControlIconKind.VerticalLayout)]
    [InlineData("CollectionView", ControlIconKind.List)]
    public void Toolbox_icons_match_control_semantics(string name, ControlIconKind expected) =>
        Assert.Equal(expected, ControlIconClassifier.ForControlName(name));

    [Theory]
    [InlineData(260, 80, true, 340)]
    [InlineData(320, 80, false, 240)]
    [InlineData(200, -100, true, SidebarResizePolicy.MinimumWidth)]
    [InlineData(500, -100, false, SidebarResizePolicy.MaximumWidth)]
    public void Sidebar_resize_tracks_each_inner_edge_and_clamps(
        double startingWidth,
        double horizontalChange,
        bool resizeFromRightEdge,
        double expected) =>
        Assert.Equal(
            expected,
            SidebarResizePolicy.CalculateWidth(
                startingWidth,
                horizontalChange,
                resizeFromRightEdge));

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
