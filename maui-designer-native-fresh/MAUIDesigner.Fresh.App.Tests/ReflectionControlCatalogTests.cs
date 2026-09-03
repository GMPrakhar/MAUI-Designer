using System.Reflection;
using MAUIDesigner.Fresh.App.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class ReflectionControlCatalogTests
{
    [Fact]
    public void Only_visual_content_properties_accept_designer_children()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(Label).Assembly);
        catalog.RegisterAssembly(Assembly.GetExecutingAssembly());

        ControlDescriptor label = Find(catalog, typeof(Label));
        ControlDescriptor contentView = Find(catalog, typeof(ContentView));
        ControlDescriptor custom = Find(catalog, typeof(CustomContainer));

        Assert.False(label.AcceptsChildren);
        Assert.False(label.Properties.Single(property => property.Name == nameof(Label.Text)).IsContent);
        Assert.True(contentView.AcceptsChildren);
        Assert.True(contentView.Properties.Single(property =>
            property.Name == nameof(ContentView.Content)).IsContent);
        Assert.True(custom.AcceptsChildren);
        Assert.True(custom.Properties.Single(property =>
            property.Name == nameof(CustomContainer.Body)).IsContent);
    }

    private static ControlDescriptor Find(ReflectionControlCatalog catalog, Type type) =>
        catalog.Controls.Single(control => control.RuntimeType == type);

}

[ContentProperty(nameof(Body))]
public sealed class CustomContainer : ContentView
{
    public View? Body { get; set; }
}
