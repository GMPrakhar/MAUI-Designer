using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.PropertyEditing;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class GridDefinitionsPropertyEditorTests
{
    [Theory]
    [InlineData("Auto,*,2*,120", "Auto,*,2*,120")]
    [InlineData("auto, 1*, 80.5", "Auto,*,80.5")]
    [InlineData("", "")]
    public void Serializer_round_trips_maui_compatible_collection_strings(
        string input,
        string expected)
    {
        Assert.True(GridDefinitionSerializer.TryParse(input, out GridTrackDefinition[] values));

        Assert.Equal(expected, GridDefinitionSerializer.Serialize(values));
    }

    [Theory]
    [InlineData("Auto,,*")]
    [InlineData("-1")]
    [InlineData("NaN*")]
    [InlineData("wat")]
    public void Serializer_rejects_invalid_grid_lengths(string input)
    {
        Assert.False(GridDefinitionSerializer.TryParse(input, out _));
    }

    [Fact]
    public void Specialized_editor_recognizes_grid_definition_collections()
    {
        var editor = new GridDefinitionsPropertyEditor();
        var property = new PropertyDescriptor(
            nameof(Grid.RowDefinitions),
            typeof(RowDefinitionCollection),
            true,
            false,
            false,
            false);

        Assert.True(editor.CanEdit(property));
    }

    [Fact]
    public void Add_and_remove_commit_canonical_serialized_values()
    {
        var model = new GridDefinitionCollectionModel("80");

        Assert.Equal("80,*", model.Add());

        Assert.Equal("*", model.RemoveAt(0));
    }
}
