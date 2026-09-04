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

    [Fact]
    public void Maui_xaml_names_resolve_to_modern_controls_not_compatibility_shims()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);

        ControlDescriptor grid = Assert.Single(catalog.Controls, control =>
            control.Id.XamlNamespace == "http://schemas.microsoft.com/dotnet/2021/maui" &&
            control.Id.XamlName == nameof(Grid));

        Assert.Equal(typeof(Grid), grid.RuntimeType);
        Assert.DoesNotContain(
            catalog.Controls,
            control => control.RuntimeType.Namespace?.Contains(
                ".Compatibility",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Catalog_uses_its_service_provider_for_registered_factories()
    {
        ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var catalog = new ReflectionControlCatalog(services);
        catalog.RegisterAssembly(typeof(View).Assembly);
        IServiceProvider? observedProvider = null;
        catalog.RegisterFactory<InjectedControl>(provider =>
        {
            observedProvider = provider;
            throw new FactoryProbeException();
        });
        ControlDescriptor injected = Find(catalog, typeof(InjectedControl));
        Assert.Throws<FactoryProbeException>(() =>
            catalog.Create(injected.Id));

        Assert.Same(services, observedProvider);
    }

    private static ControlDescriptor Find(ReflectionControlCatalog catalog, Type type) =>
        catalog.Controls.Single(control => control.RuntimeType == type);

}

[ContentProperty(nameof(Body))]
public sealed class CustomContainer : ContentView
{
    public View? Body { get; set; }
}

public sealed class InjectedControl : Label
{
    public InjectedControl(string requiredValue) => RequiredValue = requiredValue;

    public string RequiredValue { get; }
}

public sealed class FactoryProbeException : Exception;
