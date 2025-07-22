namespace MAUIDesigner.Core.Properties
{
    /// <summary>
    /// Interface for property management operations
    /// </summary>
    public interface IPropertyService
    {
        /// <summary>
        /// Populates the property view for a focused element
        /// </summary>
        /// <param name="propertyDisplayLayout">The layout to populate with property controls</param>
        /// <param name="focusedView">The view whose properties to display</param>
        void PopulatePropertyView(Layout propertyDisplayLayout, View focusedView);
        
        /// <summary>
        /// Gets organized properties for a view
        /// </summary>
        /// <param name="view">The view to get properties for</param>
        /// <returns>Properties grouped by category</returns>
        IEnumerable<PropertyGroup> GetOrganizedPropertiesForView(View view);
    }

    /// <summary>
    /// Represents a group of properties by category
    /// </summary>
    public class PropertyGroup
    {
        public PropertyCategory Category { get; set; }
        public System.Reflection.PropertyInfo[] Properties { get; set; } = Array.Empty<System.Reflection.PropertyInfo>();
    }
}