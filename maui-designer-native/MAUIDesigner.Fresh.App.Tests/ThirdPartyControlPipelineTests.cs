using System.Runtime.Loader;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Hosting;
using MAUIDesigner.Fresh.App.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class ThirdPartyControlPipelineTests
{
    [Fact]
    public void Arbitrary_control_assemblies_load_without_a_vendor_adapter()
    {
        string controlsPath = Path.Combine(AppContext.BaseDirectory, "DesignerFixture.Controls.dll");
        string dependencyPath = Path.Combine(AppContext.BaseDirectory, "DesignerFixture.Dependency.dll");
        var payload = new HostedProjectControls(
            "net10.0-windows10.0.19041.0/win-x64",
            [],
            [
                new HostedRuntimeAssembly(
                    controlsPath,
                    "Acme.Widgets",
                    true),
                new HostedRuntimeAssembly(
                    dependencyPath,
                    "Acme.Widget.Foundation",
                    false)
            ],
            [],
            []);
        var runtime = new DesignerPackageRuntime();

        IReadOnlyList<System.Reflection.Assembly> loaded = runtime.Load(payload);
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);
        foreach (System.Reflection.Assembly assembly in loaded)
        {
            catalog.RegisterAssembly(assembly);
        }

        ControlDescriptor descriptor = Assert.Single(catalog.Controls, control =>
            control.Id.FullName == "DesignerFixture.Controls.FixtureControl");
        Assert.Equal("DesignerFixture.Controls", descriptor.Id.AssemblyName);
        Assert.Empty(payload.StartupMethods);
    }

    [Fact]
    public void Runtime_closure_startup_catalog_xaml_and_materialization_are_connected()
    {
        string controlsPath = Path.Combine(AppContext.BaseDirectory, "DesignerFixture.Controls.dll");
        string dependencyPath = Path.Combine(AppContext.BaseDirectory, "DesignerFixture.Dependency.dll");
        var payload = new HostedProjectControls(
            "net10.0-windows10.0.19041.0/win-x64",
            [],
            [
                new HostedRuntimeAssembly(
                    controlsPath,
                    "DesignerFixture.Controls",
                    true),
                new HostedRuntimeAssembly(
                    dependencyPath,
                    "DesignerFixture.Dependency",
                    false)
            ],
            [
                new HostedStartupMethod(
                    "DesignerFixture.Controls",
                    "DesignerFixture.Controls",
                    "DesignerFixture.Controls.FixtureHostingExtensions",
                    "ConfigureFixture")
            ],
            []);
        var runtime = new DesignerPackageRuntime();

        IReadOnlyList<System.Reflection.Assembly> loaded = runtime.Load(payload);
        runtime.Configure(MauiApp.CreateBuilder(), payload);

        var services = new ServiceCollection().BuildServiceProvider();
        var catalog = new ReflectionControlCatalog(services);
        catalog.RegisterAssembly(typeof(View).Assembly);
        foreach (System.Reflection.Assembly assembly in loaded)
        {
            catalog.RegisterAssembly(assembly);
        }

        ControlDescriptor descriptor = catalog.Controls.Single(control =>
            control.Id.FullName == "DesignerFixture.Controls.FixtureControl");
        Assert.Equal(
            "clr-namespace:DesignerFixture.Controls;assembly=DesignerFixture.Controls",
            descriptor.Id.XamlNamespace);

        var xaml = new XamlWorkspace(new CatalogXamlTypeResolver(catalog));
        var parsed = xaml.Parse("""
            <fixture:FixtureControl
                xmlns:fixture="clr-namespace:DesignerFixture.Controls;assembly=DesignerFixture.Controls" />
            """);
        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));

        AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(loaded.Single())!;
        System.Reflection.Assembly dependency = context.Assemblies.Single(assembly =>
            assembly.GetName().Name == "DesignerFixture.Dependency");
        Type marker = dependency.GetType("DesignerFixture.Dependency.FixtureMarker", true)!;
        Assert.True((bool)marker.GetProperty("Configured")!.GetValue(null)!);
        Assert.False((bool)marker.GetProperty("Constructed")!.GetValue(null)!);
        Assert.NotNull(descriptor.Factory);
    }

    [Fact]
    public void Unresolved_clr_control_preserves_xaml_and_materializes_a_clear_placeholder()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);
        var xaml = new XamlWorkspace(new CatalogXamlTypeResolver(catalog));
        var parsed = xaml.Parse("""
            <missing:SfButton
                xmlns:missing="clr-namespace:Syncfusion.Maui.Buttons;assembly=Syncfusion.Maui.Buttons"
                Text="Preserve me" />
            """);

        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Contains("Text=\"Preserve me\"", xaml.Write(parsed.Document!));

        Assert.False(catalog.TryGet(parsed.Document!.Root.ControlType, out _));
    }
}
