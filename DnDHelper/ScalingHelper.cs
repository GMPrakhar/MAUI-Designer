using Microsoft.Maui.Controls.Compatibility.Platform.UWP;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamls = Microsoft.UI.Xaml;

namespace MAUIDesigner.DnDHelper
{
    internal static class ScalingHelper
    {
        internal static void ScaleView(View? draggingView, Point location, ScaleDirection scaleDirection)
        {
            if (draggingView == null)
            {
                return;
            }

            // Check if the dragging view is positioned in a OwningGrid layout
            bool isInGrid = draggingView.Parent is Grid;

            // Get current dimensions and position
            double currentWidth = draggingView.WidthRequest > 0 ? draggingView.WidthRequest : draggingView.Width;
            double currentHeight = draggingView.HeightRequest > 0 ? draggingView.HeightRequest : draggingView.Height;
            double currentX = draggingView.Margin.Left;
            double currentY = draggingView.Margin.Top;

            double newWidth = currentWidth;
            double newHeight = currentHeight;
            double newX = currentX;
            double newY = currentY;

            switch (scaleDirection)
            {
                case ScaleDirection.TopLeft:
                    // Scaling from top-left: position changes, size changes
                    newWidth = Math.Max(20, currentWidth + (currentX - location.X));
                    newHeight = Math.Max(20, currentHeight + (currentY - location.Y));
                    newX = currentX - (newWidth - currentWidth);
                    newY = currentY - (newHeight - currentHeight);
                    break;

                case ScaleDirection.TopRight:
                    // Scaling from top-right: only width and height change, Y position changes
                    newWidth = Math.Max(20, location.X - currentX);
                    newHeight = Math.Max(20, currentHeight + (currentY - location.Y));
                    newY = currentY - (newHeight - currentHeight);
                    break;

                case ScaleDirection.BottomLeft:
                    // Scaling from bottom-left: width and height change, X position changes
                    newWidth = Math.Max(20, currentWidth + (currentX - location.X));
                    newHeight = Math.Max(20, location.Y - currentY);
                    newX = currentX - (newWidth - currentWidth);
                    break;

                case ScaleDirection.BottomRight:
                    // Scaling from bottom-right: only width and height change
                    newWidth = Math.Max(20, location.X - currentX);
                    newHeight = Math.Max(20, location.Y - currentY);
                    break;
            }

            // Update the size
            draggingView.WidthRequest = newWidth;
            draggingView.HeightRequest = newHeight;
            
            // Only update margin if the element is NOT in a OwningGrid layout
            // OwningGrid elements use OwningGrid.Row/OwningGrid.Column for positioning, not margin
            if (!isInGrid)
            {
                draggingView.Margin = new Thickness(newX, newY, 0, 0);
            }
        }

        public enum ScaleDirection
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
    }
}
