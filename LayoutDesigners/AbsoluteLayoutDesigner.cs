using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.LayoutDesigners
{
    class AbsoluteLayoutDesigner : ILayoutDesigner
    {
        private AbsoluteLayout? absoluteLayout;

        public AbsoluteLayoutDesigner(AbsoluteLayout? absoluteLayout)
        {
            this.absoluteLayout = absoluteLayout;
        }

        public void OnDrop(View draggingView, Point location)
        {
            if (draggingView != null)
            {
                try
                {
                    if (draggingView.Parent.Parent != absoluteLayout)
                    {
                        (draggingView.Parent.Parent as Layout).Remove(draggingView);
                        absoluteLayout?.Children.Add(draggingView);
                    }

                    draggingView.Margin = new Thickness(location.X, location.Y, location.X + draggingView.Width, location.Y + draggingView.Height);
                }
                catch { }

            }
        }
    }
}
