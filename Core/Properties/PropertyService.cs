namespace MAUIDesigner.Core.Properties
{
    /// <summary>
    /// Service for managing property operations
    /// </summary>
    public class PropertyService : IPropertyService
    {
        private const int PropertyLabelFontSize = 11;
        private const int PropertyCategoryFontSize = 13;
        private const int PropertyGridPadding = 6;
        private const int CategoryHeaderPadding = 10;

        public void PopulatePropertyView(Layout propertyDisplayLayout, View focusedView)
        {
            propertyDisplayLayout.Clear();
            
            var propertyGroups = GetOrganizedPropertiesForView(focusedView);
            
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

        public IEnumerable<PropertyGroup> GetOrganizedPropertiesForView(View view)
        {
            return PropertyManager.GetOrganizedPropertiesForView(view);
        }

        private Frame CreateCategoryHeader(PropertyCategory category)
        {
            var categoryName = GetCategoryDisplayName(category);
            var categoryIcon = GetCategoryIcon(category);
            
            var headerContent = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Padding = new Thickness(CategoryHeaderPadding, 5),
                Children =
                {
                    new Label
                    {
                        Text = categoryIcon,
                        FontSize = PropertyCategoryFontSize,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    },
                    new Label
                    {
                        Text = categoryName,
                        FontSize = PropertyCategoryFontSize,
                        FontAttributes = FontAttributes.Bold,
                        VerticalOptions = LayoutOptions.Center
                    }
                }
            };

            return new Frame
            {
                Content = headerContent,
                BackgroundColor = Colors.LightGray,
                Padding = 0,
                Margin = new Thickness(0, 5, 0, 0),
                CornerRadius = 3
            };
        }

        private Grid CreatePropertyGrid(string propertyName, View propertyView)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) }
                },
                Padding = new Thickness(PropertyGridPadding, 2),
                Margin = new Thickness(0, 1)
            };

            var nameLabel = new Label
            {
                Text = propertyName,
                FontSize = PropertyLabelFontSize,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start
            };

            grid.Add(nameLabel, 0, 0);
            grid.Add(propertyView, 1, 0);

            return grid;
        }

        private BoxView CreateCategorySeparator()
        {
            return new BoxView
            {
                Height = 1,
                BackgroundColor = Colors.Gray,
                Margin = new Thickness(0, 5, 0, 10),
                Opacity = 0.3
            };
        }

        private string GetCategoryDisplayName(PropertyCategory category)
        {
            return category switch
            {
                PropertyCategory.Layout => "Layout",
                PropertyCategory.Appearance => "Appearance",
                PropertyCategory.Behavior => "Behavior",
                PropertyCategory.Data => "Data",
                PropertyCategory.Text => "Text",
                PropertyCategory.Common => "Common",
                _ => "Other"
            };
        }

        private string GetCategoryIcon(PropertyCategory category)
        {
            return category switch
            {
                PropertyCategory.Layout => "📐",
                PropertyCategory.Appearance => "🎨",
                PropertyCategory.Behavior => "⚙️",
                PropertyCategory.Data => "📊",
                PropertyCategory.Text => "📝",
                PropertyCategory.Common => "⭐",
                _ => "📋"
            };
        }
    }
}