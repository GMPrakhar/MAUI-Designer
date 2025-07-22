namespace MAUIDesigner
{
    internal class PropertyHelper
    {
        private const int PropertyLabelFontSize = 10;
        private const int PropertyGridPadding = 8;

        internal static void PopulatePropertyView(Layout propertyDisplayLayout, View focusedView)
        {
            var properties = ToolBox.GetAllPropertiesForView(focusedView);
            var gridList = new SortedDictionary<string, Grid>();
            
            foreach (var property in properties)
            {
                var grid = CreatePropertyGrid(property.Key, property.Value);
                propertyDisplayLayout.Add(grid);
            }
        }

        private static Grid CreatePropertyGrid(string propertyName, View propertyValue)
        {
            var label = new Label
            {
                Text = propertyName,
                FontSize = PropertyLabelFontSize,
                VerticalTextAlignment = TextAlignment.Center,
            };

            var grid = new Grid()
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }
                },
                Padding = PropertyGridPadding,
                InputTransparent = true,
                CascadeInputTransparent = false,
                VerticalOptions = LayoutOptions.Start
            };

            grid.Add(label);
            grid.Add(propertyValue);

            grid.SetColumn(label, 0);
            grid.SetColumn(propertyValue, 1);

            grid.SetRow(label, 0);
            grid.SetRow(propertyValue, 0);

            return grid;
        }
    }
}
