using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Xaml;

namespace MAUIDesigner.Fresh.Core.Tests;

public sealed class XamlRoundTripTests
{
    private const string Maui = "http://schemas.microsoft.com/dotnet/2021/maui";
    private const string Xaml = "http://schemas.microsoft.com/winfx/2009/xaml";
    private const string Toolkit = "http://schemas.microsoft.com/dotnet/2022/maui/toolkit";
    private readonly TestResolver _resolver = new();

    [Fact]
    public void Page_resources_bindings_attached_properties_and_custom_controls_survive()
    {
        const string source = """
            <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
                         x:Class="Sample.EntryPage">
              <ContentPage.Resources>
                <Color x:Key="Accent">#112233</Color>
              </ContentPage.Resources>
              <Grid RowDefinitions="Auto,*,2*" ColumnDefinitions="80,*">
                <Label x:Name="Title" Grid.Row="1" Text="{Binding Title}" />
                <toolkit:AvatarView Grid.Row="2" Text="A &amp; B" />
              </Grid>
            </ContentPage>
            """;

        XamlReadResult result = new DesignerXamlReader().Read(source, _resolver);

        Assert.True(result.Success);
        Assert.Equal("Grid", result.Document!.Root.ControlType.XamlName);
        Assert.Equal("{Binding Title}", result.Document.Root.Children[0].Properties["Text"].Text);
        Assert.Equal(DesignerValueKind.MarkupExtension, result.Document.Root.Children[0].Properties["Text"].Kind);
        Assert.Equal("1", result.Document.Root.Children[0].Properties["Grid.Row"].Text);
        Assert.Equal("AvatarView", result.Document.Root.Children[1].ControlType.XamlName);

        string generated = new DesignerXamlWriter().Write(result.Document);
        Assert.Contains("ContentPage.Resources", generated);
        Assert.Contains("RowDefinitions=\"Auto,*,2*\"", generated);
        Assert.Contains("Text=\"{Binding Title}\"", generated);
        Assert.Contains("toolkit:AvatarView", generated);
        Assert.Contains("Text=\"A &amp; B\"", generated);
        Assert.Contains($"xmlns:toolkit=\"{Toolkit}\"", generated);
    }

    [Fact]
    public void Invalid_xml_returns_diagnostic_without_a_document()
    {
        XamlReadResult result = new DesignerXamlReader().Read("<Grid><Label></Grid>", _resolver);

        Assert.False(result.Success);
        Assert.Null(result.Document);
        Assert.NotEmpty(result.Diagnostics);
        Assert.NotNull(result.Diagnostics[0].Line);
    }

    [Fact]
    public void Unknown_control_is_rejected_with_its_source_location()
    {
        XamlReadResult result = new DesignerXamlReader().Read(
            $"""<Mystery xmlns="{Maui}" />""",
            _resolver);

        Assert.False(result.Success);
        Assert.Contains("Mystery", result.Diagnostics.Single().Message);
        Assert.Equal(1, result.Diagnostics.Single().Line);
    }

    [Fact]
    public void Non_container_controls_reject_visual_children()
    {
        XamlReadResult result = new DesignerXamlReader().Read(
            $"""<Label xmlns="{Maui}"><Label /></Label>""",
            _resolver);

        Assert.False(result.Success);
        Assert.Contains("cannot contain visual children", result.Diagnostics.Single().Message);
    }

    [Fact]
    public void Named_visual_slots_reject_duplicate_children()
    {
        XamlReadResult result = new DesignerXamlReader().Read(
            $"""
            <Expander xmlns="{Maui}">
              <Expander.Header>
                <Label Text="First" />
                <Label Text="Second" />
              </Expander.Header>
            </Expander>
            """,
            _resolver);

        Assert.False(result.Success);
        Assert.Contains("accepts only one child", result.Diagnostics.Single().Message);
    }

    [Fact]
    public void Repeated_named_visual_property_elements_are_rejected()
    {
        XamlReadResult result = new DesignerXamlReader().Read(
            $"""
            <Expander xmlns="{Maui}">
              <Expander.Header><Label Text="First" /></Expander.Header>
              <Expander.Header><Label Text="Second" /></Expander.Header>
            </Expander>
            """,
            _resolver);

        Assert.False(result.Success);
        Assert.Contains("assigned more than once", result.Diagnostics.Single().Message);
    }

    [Fact]
    public void Absolute_bounds_and_descendant_namespace_declarations_round_trip()
    {
        const string source = """
            <Grid xmlns="http://schemas.microsoft.com/dotnet/2021/maui">
              <Label xmlns:dock="urn:sample:dock"
                     dock:Panel.Position="Left"
                     AbsoluteLayout.LayoutBounds="12,24,160,48"
                     AbsoluteLayout.LayoutFlags="None" />
            </Grid>
            """;

        XamlReadResult parsed = new DesignerXamlReader().Read(source, _resolver);

        Assert.True(parsed.Success, string.Join(Environment.NewLine, parsed.Diagnostics));
        Assert.Equal(new MAUIDesigner.Fresh.Core.Geometry.RectD(12, 24, 160, 48),
            Assert.Single(parsed.Document!.Root.Children).Bounds);

        string generated = new DesignerXamlWriter().Write(parsed.Document);
        Assert.Contains("xmlns:dock=\"urn:sample:dock\"", generated);
        Assert.Contains("dock:Panel.Position=\"Left\"", generated);
        Assert.Contains("AbsoluteLayout.LayoutBounds=\"12,24,160,48\"", generated);

        XamlReadResult reparsed = new DesignerXamlReader().Read(generated, _resolver);
        Assert.True(reparsed.Success, string.Join(Environment.NewLine, reparsed.Diagnostics));
        Assert.Equal(
            parsed.Document.Root.Children[0].Bounds,
            reparsed.Document!.Root.Children[0].Bounds);
    }

    private sealed class TestResolver : IXamlTypeResolver
    {
        public bool TryResolve(
            string xamlNamespace,
            string localName,
            out XamlTypeResolution? resolution)
        {
            bool known = localName is "ContentPage" or "Grid" or "Label" or "AvatarView" or "Expander";
            if (!known)
            {
                resolution = null;
                return false;
            }

            bool toolkit = xamlNamespace == Toolkit;
            var type = new ControlTypeId(
                toolkit ? "CommunityToolkit.Maui" : "Microsoft.Maui.Controls",
                toolkit
                    ? $"CommunityToolkit.Maui.Views.{localName}"
                    : $"Microsoft.Maui.Controls.{localName}",
                xamlNamespace,
                localName);
            resolution = new XamlTypeResolution(
                type,
                localName != "ContentPage",
                localName switch
                {
                    "ContentPage" => "Content",
                    "Grid" => "Children",
                    "Expander" => "Content",
                    _ => null
                },
                localName == "Expander" ? ["Header"] : default,
                localName is "Grid" or "Expander");
            return true;
        }
    }
}
