using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner
{
    internal class PropertyHelper
    {
        internal static void PopulatePropertyView(Layout propertyDisplayLayout, View focusedView)
        {
            var properties = ToolBox.GetAllPropertiesForView(focusedView);
            var gridList = new SortedDictionary<string, Grid>();
            foreach (var property in properties)
            {
                var label = new Label
                {
                    Text = property.Key,
                    FontSize = 10,
                    VerticalTextAlignment = TextAlignment.Center,
                };

                var value = property.Value;
                // Put the label and value in a grid layout
                var grid = new Grid()
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }
                },
                    Padding = 8,
                    InputTransparent = true,
                    CascadeInputTransparent = false,

                };

                grid.VerticalOptions = LayoutOptions.Start;

                grid.Add(label);
                grid.Add(value);

                grid.SetColumn(label, 0);
                grid.SetColumn(value, 1);

                grid.SetRow(label, 0);
                grid.SetRow(value, 0);

                propertyDisplayLayout.Add(grid);
            }
        }
    }
}
