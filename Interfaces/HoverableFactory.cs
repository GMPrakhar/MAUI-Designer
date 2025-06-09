using MAUIDesigner.LayoutDesigners;
using MAUIDesigner.NewFolder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.Interfaces
{
    internal static class HoverableFactory
    {
        public static IHoverable GetHoverController(IView view)
        {
            switch (view)
            {
                case Grid grid:
                    return LayoutDesignerFactory.CreateLayoutDesigner(grid);
                default:
                    throw new NotSupportedException($"Hoverable controller for {view.GetType().Name} is not supported.");
            }
        }
    }
}
