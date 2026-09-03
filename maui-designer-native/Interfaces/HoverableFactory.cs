using MAUIDesigner.LayoutDesigners;

namespace MAUIDesigner.Interfaces
{
    internal static class HoverableFactory
    {
        public static IHoverable GetHoverController(IView view)
        {
            return view switch
            {
                Grid grid => LayoutDesignerFactory.CreateLayoutDesigner(grid),
                _ => throw new NotSupportedException($"Hoverable controller for {view.GetType().Name} is not supported.")
            };
        }
    }
}
