using System.ComponentModel;
using System.Linq;

using MauiDesigner.Core.Protocol;

using Xunit;

namespace MauiDesigner.Core.Tests
{
    public sealed class DesignerSelectionProxyTests
    {
        [Fact]
        public void Exposes_typed_properties_and_forwards_edits()
        {
            string? changedName = null;
            string? changedValue = null;
            var snapshot = new DesignerSelectionSnapshot
            {
                SelectionCount = 1,
                ElementId = "button-1",
                DisplayName = "Button",
                Properties =
                {
                    new DesignerPropertySnapshot
                    {
                        Name = "IsEnabled",
                        Value = "true",
                        ValueType = typeof(bool).FullName!,
                        Category = "Behavior"
                    }
                }
            };
            var proxy = new DesignerSelectionProxy(
                snapshot,
                (name, value) =>
                {
                    changedName = name;
                    changedValue = value;
                });

            PropertyDescriptor property = Assert.Single(
                ((ICustomTypeDescriptor)proxy).GetProperties().Cast<PropertyDescriptor>());

            Assert.Equal(typeof(bool), property.PropertyType);
            Assert.Equal(true, property.GetValue(proxy));
            Assert.Equal("Behavior", property.Category);

            property.SetValue(proxy, false);

            Assert.Equal("IsEnabled", changedName);
            Assert.Equal("False", changedValue);
            Assert.Equal(false, property.GetValue(proxy));
        }

        [Fact]
        public void Grid_definitions_expose_the_visual_studio_modal_editor()
        {
            string? changedValue = null;
            var snapshot = new DesignerSelectionSnapshot
            {
                SelectionCount = 1,
                ElementId = "grid-1",
                DisplayName = "Grid",
                Properties =
                {
                    new DesignerPropertySnapshot
                    {
                        Name = "RowDefinitions",
                        Value = "Auto,*",
                        ValueType = "Microsoft.Maui.Controls.RowDefinitionCollection",
                        Category = "Layout"
                    }
                }
            };
            var proxy = new DesignerSelectionProxy(
                snapshot,
                (_, value) => changedValue = value,
                typeof(object));

            PropertyDescriptor property = Assert.Single(
                ((ICustomTypeDescriptor)proxy).GetProperties().Cast<PropertyDescriptor>());
            Assert.Equal(typeof(DesignerSelectionProxy), property.ComponentType);
            Assert.Equal(typeof(DesignerGridDefinitionValue), property.PropertyType);
            Assert.Equal("(Collection)", property.GetValue(proxy)!.ToString());

            property.SetValue(
                proxy,
                new DesignerGridDefinitionValue("1*,Auto"));

            Assert.Equal("1*,Auto", changedValue);
        }

        [Fact]
        public void Enum_properties_expose_an_exclusive_dropdown_and_forward_selection()
        {
            string? changedValue = null;
            var snapshot = new DesignerSelectionSnapshot
            {
                SelectionCount = 1,
                ElementId = "label-1",
                DisplayName = "Label",
                Properties =
                {
                    new DesignerPropertySnapshot
                    {
                        Name = "HorizontalOptions",
                        Value = "Fill",
                        ValueType = "Microsoft.Maui.Controls.LayoutOptions",
                        Category = "Layout",
                        EnumValues = new() { "Start", "Center", "End", "Fill" }
                    }
                }
            };
            var proxy = new DesignerSelectionProxy(
                snapshot,
                (_, value) => changedValue = value);

            PropertyDescriptor property = Assert.Single(
                ((ICustomTypeDescriptor)proxy).GetProperties().Cast<PropertyDescriptor>());
            TypeConverter converter = property.Converter;

            Assert.True(converter.GetStandardValuesSupported());
            Assert.True(converter.GetStandardValuesExclusive());
            Assert.Equal(
                new[] { "Start", "Center", "End", "Fill" },
                converter.GetStandardValues()!
                    .Cast<DesignerEnumValue>()
                    .Select(value => value.Value));

            property.SetValue(proxy, new DesignerEnumValue("Center"));

            Assert.Equal("Center", changedValue);
        }
    }
}
