using System.Reflection;
using CommunityToolkit.Maui.Views;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Xaml;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class ComplexXamlIntegrationTests
{
    [Theory]
    [InlineData("{Binding PlanName, FallbackValue=Professional}", "Professional")]
    [InlineData("{Binding Price, FallbackValue='Contact, sales'}", "Contact, sales")]
    public void Binding_fallbacks_supply_design_time_preview_text(
        string markup,
        string expected)
    {
        Assert.True(DesignerMarkupPreview.TryGetLiteral(markup, out string preview));
        Assert.Equal(expected, preview);
    }

    [Fact]
    public void Toolkit_visual_property_elements_are_rendered_and_round_trip()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        var xaml = new XamlWorkspace(new CatalogXamlTypeResolver(catalog));
        string source = ReadFixture();

        XamlReadResult parsed = xaml.Parse(source);

        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        DesignerNode expander = Assert.Single(
            parsed.Document!.Root.Children,
            child => child.ControlType.XamlName == nameof(Expander));
        Assert.Equal(nameof(Expander), expander.ControlType.XamlName);
        DesignerNode header = Assert.Single(
            expander.Children,
            child => child.ParentPropertyName == nameof(Expander.Header));
        Assert.Equal(nameof(Label), header.ControlType.XamlName);
        Assert.Contains(
            expander.Children,
            child =>
                child.ParentPropertyName is null &&
                child.ControlType.XamlName == nameof(VerticalStackLayout));
        Assert.True(DesignerMarkupPreview.TryGetLiteral(
            parsed.Document.Find(new ElementId("PlanPreview"))!
                .Properties["Text"]
                .Text,
            out string bindingPreview));
        Assert.Equal("Professional", bindingPreview);

        string generated = xaml.Write(parsed.Document!);
        Assert.Contains("toolkit:Expander.Header", generated);
        Assert.Contains("toolkit:AvatarView", generated);
        Assert.Contains("toolkit:TextValidationBehavior", generated);
        Assert.Contains("ContentPage.Resources", generated);
        Assert.Contains("Style=\"{StaticResource TitleStyle}\"", generated);
    }

    [Fact]
    public void Runtime_loaded_control_assembly_uses_its_official_xaml_namespace()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);
        var loader = new AssemblyExtensionLoader(catalog);

        ExtensionLoadResult loaded = loader.Load(typeof(ExternalGauge).Assembly.Location);

        Assert.True(loaded.ControlsAdded > 0);
        ControlDescriptor external = catalog.Controls.Single(control =>
            control.RuntimeType.FullName == typeof(ExternalGauge).FullName);
        Assert.Equal("urn:maui-designer:test-controls", external.Id.XamlNamespace);

        var xaml = new XamlWorkspace(new CatalogXamlTypeResolver(catalog));
        XamlReadResult parsed = xaml.Parse("""
            <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:external="urn:maui-designer:test-controls">
              <external:ExternalGauge Value="72">
                <Label Text="External library content" />
              </external:ExternalGauge>
            </ContentPage>
            """);

        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal("ExternalGauge", parsed.Document!.Root.ControlType.XamlName);
        Assert.Equal("72", parsed.Document.Root.Properties[nameof(ExternalGauge.Value)].Text);
        Assert.Equal("Label", Assert.Single(parsed.Document.Root.Children).ControlType.XamlName);
        Assert.Contains(
            "xmlns:external=\"urn:maui-designer:test-controls\"",
            xaml.Write(parsed.Document));
    }

    private static ReflectionControlCatalog CreateCatalog()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);
        catalog.RegisterAssembly(typeof(AvatarView).Assembly);
        return catalog;
    }

    private static string ReadFixture()
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ComplexToolkitPage.xaml")
            ?? throw new InvalidOperationException("Complex Toolkit XAML fixture is unavailable.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

public sealed class ExternalGauge : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(double),
        typeof(ExternalGauge),
        0d);

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
}
