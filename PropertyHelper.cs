namespace MAUIDesigner
{
    internal class PropertyHelper
    {
        private const int PropertyLabelFontSize = 11;
        private const int PropertyCategoryFontSize = 13;
        private const int PropertyGridPadding = 6;
        private const int CategoryHeaderPadding = 10;

        internal static void PopulatePropertyView(Layout propertyDisplayLayout, View focusedView)
        {
            propertyDisplayLayout.Clear();
            
            var propertyGroups = PropertyManager.GetOrganizedPropertiesForView(focusedView);
            
            foreach (var group in propertyGroups)
            {
                if (group.Properties.Length == 0) continue;
                
                // Add category header
                var categoryHeader = CreateCategoryHeader(group.Category);
                propertyDisplayLayout.Add(categoryHeader);
                
                // Add properties in this category
                foreach (var property in group.Properties)
                {
                    try
                    {
                        var propertyValue = property.GetValue(focusedView);
                        var propertyView = ToolBox.GetViewForPropertyType(focusedView, property, propertyValue);
                        var grid = CreatePropertyGrid(property.Name, propertyView);
                        propertyDisplayLayout.Add(grid);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating property view for {property.Name}: {ex.Message}");
                    }
                }
                
                // Add separator between categories
                var separator = CreateCategorySeparator();
                propertyDisplayLayout.Add(separator);
            }
        }

        private static Frame CreateCategoryHeader(PropertyCategory category)
        {
            var categoryName = GetCategoryDisplayName(category);
            var categoryIcon = GetCategoryIcon(category);
            
            var stackLayout = new HorizontalStackLayout
            {
                Spacing = 8,
                VerticalOptions = LayoutOptions.Center
            };
            
            if (!string.IsNullOrEmpty(categoryIcon))
            {
                var icon = new Label
                {
                    Text = categoryIcon,
                    FontSize = PropertyCategoryFontSize,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = GetCategoryColor(category)
                };
                stackLayout.Children.Add(icon);
            }
            
            var label = new Label
            {
                Text = categoryName,
                FontSize = PropertyCategoryFontSize,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.DarkGray
            };
            stackLayout.Children.Add(label);
            
            return new Frame
            {
                Content = stackLayout,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromRgba(40, 40, 40, 100) : Color.FromRgba(245, 245, 245, 100),
                BorderColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromRgba(80, 80, 80, 150) : Color.FromRgba(200, 200, 200, 150),
                CornerRadius = 4,
                Padding = new Thickness(CategoryHeaderPadding, 6),
                Margin = new Thickness(0, 8, 0, 4),
                HasShadow = false
            };
        }
        
        private static BoxView CreateCategorySeparator()
        {
            return new BoxView
            {
                Color = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromRgba(60, 60, 60, 80) : Color.FromRgba(220, 220, 220, 80),
                HeightRequest = 1,
                Margin = new Thickness(0, 4, 0, 8)
            };
        }

        private static Grid CreatePropertyGrid(string propertyName, View propertyValue)
        {
            var label = new Label
            {
                Text = propertyName,
                FontSize = PropertyLabelFontSize,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Black,
                LineBreakMode = LineBreakMode.TailTruncation,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var grid = new Grid()
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition() { Width = new GridLength(0.4, GridUnitType.Star) },
                    new ColumnDefinition() { Width = new GridLength(0.6, GridUnitType.Star) }
                },
                Padding = new Thickness(PropertyGridPadding, 4),
                InputTransparent = true,
                CascadeInputTransparent = false,
                VerticalOptions = LayoutOptions.Start,
                BackgroundColor = Colors.Transparent
            };

            grid.Add(label);
            grid.Add(propertyValue);

            grid.SetColumn(label, 0);
            grid.SetColumn(propertyValue, 1);

            grid.SetRow(label, 0);
            grid.SetRow(propertyValue, 0);

            return grid;
        }
        
        private static string GetCategoryDisplayName(PropertyCategory category)
        {
            return category switch
            {
                PropertyCategory.Layout => "Layout",
                PropertyCategory.Appearance => "Appearance", 
                PropertyCategory.Text => "Text",
                PropertyCategory.Behavior => "Behavior",
                PropertyCategory.Other => "Other",
                _ => category.ToString()
            };
        }
        
        private static string GetCategoryIcon(PropertyCategory category)
        {
            return category switch
            {
                PropertyCategory.Layout => "⊞",
                PropertyCategory.Appearance => "🎨",
                PropertyCategory.Text => "📝",
                PropertyCategory.Behavior => "⚙️",
                PropertyCategory.Other => "📋",
                _ => "•"
            };
        }
        
        private static Color GetCategoryColor(PropertyCategory category)
        {
            return category switch
            {
                PropertyCategory.Layout => Colors.Orange,
                PropertyCategory.Appearance => Colors.Purple,
                PropertyCategory.Text => Colors.Blue,
                PropertyCategory.Behavior => Colors.Green,
                PropertyCategory.Other => Colors.Gray,
                _ => Colors.Black
            };
        }
    }
}
