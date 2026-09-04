using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace MAUIDesigner.LayoutDesigners
{
    static class LayoutDesignerFactory
    {
        public static ILayoutDesigner CreateLayoutDesigner(Layout layout)
        {
            return layout switch
            {
                Grid => new GridLayoutDesigner(layout as Grid),
                AbsoluteLayout => new AbsoluteLayoutDesigner(layout as AbsoluteLayout),
                _ => new BaseLayoutDesigner(layout),
            };
        }
    }
}
