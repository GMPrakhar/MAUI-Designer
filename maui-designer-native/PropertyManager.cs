using Microsoft.Maui.Controls;
using System.Collections.Concurrent;
using System.Reflection;

namespace MAUIDesigner
{
    /// <summary>
    /// Manages property operations with improved performance and organization
    /// </summary>
    internal class PropertyManager
    {
        private static readonly ConcurrentDictionary<Type, PropertyMetadata[]> PropertyCache = new();
        
        /// <summary>
        /// Gets organized properties for a view with caching for better performance
        /// </summary>
        internal static PropertyGroup[] GetOrganizedPropertiesForView(View view)
        {
            var properties = GetCachedProperties(view.GetType());
            var groups = new Dictionary<PropertyCategory, List<PropertyInfo>>();
            
            foreach (var property in properties)
            {
                var category = property.Category;
                if (!groups.ContainsKey(category))
                {
                    groups[category] = new List<PropertyInfo>();
                }
                groups[category].Add(property.PropertyInfo);
            }
            
            return groups.Select(g => new PropertyGroup 
            { 
                Category = g.Key, 
                Properties = g.Value.ToArray() 
            }).OrderBy(g => (int)g.Category).ToArray();
        }
        
        /// <summary>
        /// Gets cached property metadata for a type
        /// </summary>
        private static PropertyMetadata[] GetCachedProperties(Type viewType)
        {
            return PropertyCache.GetOrAdd(viewType, type =>
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && IsEditableProperty(p))
                    .Select(p => new PropertyMetadata 
                    { 
                        PropertyInfo = p, 
                        Category = GetPropertyCategory(p) 
                    })
                    .ToArray();
                    
                return properties;
            });
        }
        
        /// <summary>
        /// Determines if a property should be editable in the property panel
        /// </summary>
        private static bool IsEditableProperty(PropertyInfo property)
        {
            // Skip properties that are typically not editable or useful
            var skipProperties = new HashSet<string>
            {
                "Handler", "Parent", "Window", "LogicalChildren", "VisualChildren",
                "Navigation", "BindingContext", "Resources", "StyleId", "ClassId",
                "AutomationId", "Effects", "Triggers", "Behaviors", "GestureRecognizers"
            };
            
            return !skipProperties.Contains(property.Name) && 
                   !property.PropertyType.IsSubclassOf(typeof(Element)) &&
                   property.PropertyType != typeof(IList<IVisualTreeElement>);
        }
        
        /// <summary>
        /// Categorizes properties for better organization
        /// </summary>
        private static PropertyCategory GetPropertyCategory(PropertyInfo property)
        {
            var layoutProperties = new HashSet<string>
            {
                "Margin", "Padding", "HorizontalOptions", "VerticalOptions", 
                "WidthRequest", "HeightRequest", "MinimumWidthRequest", "MinimumHeightRequest",
                "MaximumWidthRequest", "MaximumHeightRequest", "Spacing", "RowSpacing", "ColumnSpacing"
            };
            
            var appearanceProperties = new HashSet<string>
            {
                "BackgroundColor", "Background", "Opacity", "IsVisible", "Rotation",
                "RotationX", "RotationY", "Scale", "ScaleX", "ScaleY", "TranslationX", "TranslationY",
                "AnchorX", "AnchorY", "BorderColor", "CornerRadius", "Shadow", "Stroke", "Fill"
            };
            
            var textProperties = new HashSet<string>
            {
                "Text", "TextColor", "FontFamily", "FontSize", "FontAttributes",
                "TextDecorations", "LineBreakMode", "MaxLines", "LineHeight",
                "CharacterSpacing", "HorizontalTextAlignment", "VerticalTextAlignment"
            };
            
            var behaviorProperties = new HashSet<string>
            {
                "IsEnabled", "InputTransparent", "CascadeInputTransparent", "IsFocused",
                "TabIndex", "IsTabStop", "FlowDirection", "Clip"
            };
            
            if (layoutProperties.Contains(property.Name))
                return PropertyCategory.Layout;
            if (appearanceProperties.Contains(property.Name))
                return PropertyCategory.Appearance;
            if (textProperties.Contains(property.Name))
                return PropertyCategory.Text;
            if (behaviorProperties.Contains(property.Name))
                return PropertyCategory.Behavior;
                
            return PropertyCategory.Other;
        }
    }
    
    /// <summary>
    /// Metadata for a property including its category
    /// </summary>
    internal class PropertyMetadata
    {
        public PropertyInfo PropertyInfo { get; set; } = null!;
        public PropertyCategory Category { get; set; }
    }
    
    /// <summary>
    /// A group of properties in the same category
    /// </summary>
    internal class PropertyGroup
    {
        public PropertyCategory Category { get; set; }
        public PropertyInfo[] Properties { get; set; } = Array.Empty<PropertyInfo>();
    }
    
    /// <summary>
    /// Categories for organizing properties
    /// </summary>
    internal enum PropertyCategory
    {
        Layout = 1,
        Appearance = 2,
        Text = 3,
        Behavior = 4,
        Other = 5
    }
}