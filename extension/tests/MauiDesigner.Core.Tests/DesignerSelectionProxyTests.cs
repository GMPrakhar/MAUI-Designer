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
            const string editorType = "MauiDesigner.Vsix.GridDefinitionsEditor, MauiDesigner.Vsix";
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
            var proxy = new DesignerSelectionProxy(snapshot, (_, _) => { }, editorType);

            PropertyDescriptor property = Assert.Single(
                ((ICustomTypeDescriptor)proxy).GetProperties().Cast<PropertyDescriptor>());
            var editor = (EditorAttribute)property.Attributes[typeof(EditorAttribute)]!;

            Assert.Equal(editorType, editor.EditorTypeName);
        }
    }
}
