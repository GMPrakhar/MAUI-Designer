namespace MAUIDesigner.Core.Elements
{
    /// <summary>
    /// Interface for creating UI elements in the designer
    /// </summary>
    public interface IElementFactory
    {
        /// <summary>
        /// Creates a new UI element of the specified type
        /// </summary>
        /// <returns>A new View instance</returns>
        View CreateElement();
        
        /// <summary>
        /// Gets the display name for this element type
        /// </summary>
        string DisplayName { get; }
        
        /// <summary>
        /// Gets the category this element belongs to
        /// </summary>
        string Category { get; }
    }
}