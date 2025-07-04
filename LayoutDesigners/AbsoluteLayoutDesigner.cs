using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace MAUIDesigner.LayoutDesigners
{
    class AbsoluteLayoutDesigner : ILayoutDesigner
    {
        private Microsoft.Maui.Controls.AbsoluteLayout? absoluteLayout;

        public AbsoluteLayoutDesigner(Microsoft.Maui.Controls.AbsoluteLayout? absoluteLayout)
        {
            this.absoluteLayout = absoluteLayout;
        }

        public void OnDrop(View draggingView, Point location)
        {
            if (draggingView != null)
            {
                try
                {
                    if (draggingView.Parent?.Parent != absoluteLayout)
                    {
                        (draggingView.Parent?.Parent as Microsoft.Maui.Controls.Layout)?.Children.Remove(draggingView);
                        absoluteLayout?.Children.Add(draggingView);
                    }

                    draggingView.Margin = new Thickness(location.X, location.Y, location.X + draggingView.Width, location.Y + draggingView.Height);
                }
                catch { }
            }
        }

        public void OnHoverExit()
        {
            throw new NotImplementedException();
        }

        public void OnHoverMove(Point location)
        {
            throw new NotImplementedException();
        }
    }
}
