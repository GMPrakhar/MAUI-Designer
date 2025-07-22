namespace MAUIDesigner.Core.Elements
{
    /// <summary>
    /// Interface for element operations in the designer
    /// </summary>
    public interface IElementOperations
    {
        /// <summary>
        /// Creates an element in the designer frame
        /// </summary>
        /// <param name="mainDesignerView">The main designer layout</param>
        /// <param name="elementType">The type of element to create</param>
        void CreateElementInDesignerFrame(Layout mainDesignerView, string elementType);
        
        /// <summary>
        /// Adds gesture controls to an element designer view
        /// </summary>
        /// <param name="elementView">The element designer view to add gestures to</param>
        void AddDesignerGestureControls(MAUIDesigner.HelperViews.ElementDesignerView elementView);
    }
}