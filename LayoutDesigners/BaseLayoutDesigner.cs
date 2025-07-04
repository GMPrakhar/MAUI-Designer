using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility;
using System;

namespace MAUIDesigner.LayoutDesigners
{
    class BaseLayoutDesigner : ILayoutDesigner
    {
        private readonly Microsoft.Maui.Controls.Layout _layout;

        public BaseLayoutDesigner(Microsoft.Maui.Controls.Layout layout)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public void OnDrop(View draggingView, Point location)
        {

            if (draggingView != null)
            {
                try
                {
                    if (draggingView.Parent.Parent != _layout)
                    {
                        (draggingView.Parent.Parent as Microsoft.Maui.Controls.Layout)?.Remove(draggingView.Parent as IView);
                        _layout?.Children.Add(draggingView.Parent as IView);
                    }

                    draggingView.Margin = new Thickness(location.X, location.Y, location.X + draggingView.Width, location.Y + draggingView.Height);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"Error in OnDrop: {ex.Message}");
                }

            }
        }

        public void OnHoverMove(Point location)
        {
            // No-op for base
        }

        public void OnHoverExit()
        {
            // No-op for base
        }
    }
}
