using System.Collections.Immutable;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.PropertyEditing;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class PropertyInspectorDescriptorSourceTests
{
    [Fact]
    public void Grid_child_exposes_placement_and_span_properties_with_effective_defaults()
    {
        DesignerNode parent = Node("grid", "Grid");
        DesignerNode child = Node("label", "Label");
        ControlDescriptor descriptor = Descriptor(child.ControlType);

        IReadOnlyList<PropertyDescriptor> properties =
            PropertyInspectorDescriptorSource.Compose(descriptor, child, parent);

        Assert.Contains(properties, property => property.Name == "Grid.Row" && property.ValueType == typeof(int));
        Assert.Contains(properties, property => property.Name == "Grid.Column" && property.ValueType == typeof(int));
        Assert.Contains(properties, property => property.Name == "Grid.RowSpan" && property.ValueType == typeof(int));
        Assert.Contains(properties, property => property.Name == "Grid.ColumnSpan" && property.ValueType == typeof(int));
        Assert.Equal("0", PropertyInspectorDescriptorSource.DefaultValue("Grid.Row"));
        Assert.Equal("1", PropertyInspectorDescriptorSource.DefaultValue("Grid.RowSpan"));
    }

    [Fact]
    public void Non_grid_child_does_not_expose_grid_placement()
    {
        DesignerNode parent = Node("stack", "VerticalStackLayout");
        DesignerNode child = Node("label", "Label");

        IReadOnlyList<PropertyDescriptor> properties =
            PropertyInspectorDescriptorSource.Compose(Descriptor(child.ControlType), child, parent);

        Assert.DoesNotContain(properties, property => property.Name.StartsWith("Grid.", StringComparison.Ordinal));
    }

    [Fact]
    public void Preserved_unknown_and_attached_attributes_remain_editable()
    {
        DesignerNode child = Node("label", "Label") with
        {
            Properties = ImmutableDictionary<string, DesignerValue>.Empty
                .Add("Clicked", DesignerValue.Literal("OnClicked"))
                .Add("custom:Panel.Position", DesignerValue.Literal("Left"))
        };

        IReadOnlyList<PropertyDescriptor> properties =
            PropertyInspectorDescriptorSource.Compose(Descriptor(child.ControlType), child, null);

        Assert.Contains(properties, property => property.Name == "Clicked" && !property.IsAttached);
        Assert.Contains(properties, property => property.Name == "custom:Panel.Position" && property.IsAttached);
    }

    [Theory]
    [InlineData("Grid.Row", "-1")]
    [InlineData("Grid.Column", "1.5")]
    [InlineData("Grid.RowSpan", "0")]
    [InlineData("Grid.ColumnSpan", "-2")]
    public void Invalid_grid_placement_is_rejected(string name, string value)
    {
        var property = new PropertyDescriptor(name, typeof(int), true, false, true, false);

        bool valid = PropertyInspectorDescriptorSource.TryNormalize(
            property,
            value,
            out _,
            out string? error);

        Assert.False(valid);
        Assert.Contains("whole number", error);
    }

    private static DesignerNode Node(string id, string xamlName)
    {
        var type = new ControlTypeId(
            "Microsoft.Maui.Controls",
            $"Microsoft.Maui.Controls.{xamlName}",
            "http://schemas.microsoft.com/dotnet/2021/maui",
            xamlName);
        return new DesignerNode(new ElementId(id), type);
    }

    private static ControlDescriptor Descriptor(ControlTypeId type) =>
        new(
            type,
            typeof(Label),
            type.XamlName,
            "Test",
            false,
            _ => new Label(),
            []);
}
