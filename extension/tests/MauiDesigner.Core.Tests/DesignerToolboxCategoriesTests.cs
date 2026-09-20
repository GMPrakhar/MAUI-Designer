using MauiDesigner.Core.Protocol;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    public sealed class DesignerToolboxCategoriesTests
    {
        [Theory]
        [InlineData("Layouts", "MAUI - Layouts")]
        [InlineData(" Input ", "MAUI - Input")]
        [InlineData("", "MAUI - Other")]
        public void Builds_distinct_visual_studio_tab_names(
            string category,
            string expected)
        {
            Assert.Equal(expected, DesignerToolboxCategories.TabName(category));
        }

        [Fact]
        public void Places_primary_maui_categories_before_custom_categories()
        {
            Assert.True(
                DesignerToolboxCategories.Order("Layouts") <
                DesignerToolboxCategories.Order("Input"));
            Assert.True(
                DesignerToolboxCategories.Order("Input") <
                DesignerToolboxCategories.Order("Third party"));
        }
    }
}
