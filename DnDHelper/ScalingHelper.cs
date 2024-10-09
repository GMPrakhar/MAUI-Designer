using Microsoft.Maui.Controls.Compatibility.Platform.UWP;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            location.X = (int)location.X;
            location.Y = (int)location.Y;

            Thickness scalingFactor;
            if (scaleDirection == ScaleDirection.TopLeft)
            {
                scalingFactor = new Thickness(draggingView.Margin.Left - location.X, draggingView.Margin.Top - location.Y);
            }
            else if (scaleDirection == ScaleDirection.TopRight)
            {
                scalingFactor = new Thickness(location.X - draggingView.Margin.Right, draggingView.Margin.Top - location.Y);

                // Set location X to be same as focused view's margin so it doesn't get updated.
                location.X = draggingView.Margin.Left;
            }
            else if (scaleDirection == ScaleDirection.BottomLeft)
            {
                scalingFactor = new Thickness(draggingView.Margin.Left - location.X, location.Y - draggingView.Margin.Bottom);
                location.Y = draggingView.Margin.Top;
            }
            else
            {
                scalingFactor = new Thickness(location.X - draggingView.Margin.Right, location.Y - draggingView.Margin.Bottom);
                location.X = draggingView.Margin.Left;
                location.Y = draggingView.Margin.Top;
            }

            draggingView.WidthRequest = Math.Max(draggingView.WidthRequest + scalingFactor.Left, 20);
            draggingView.HeightRequest = Math.Max(draggingView.HeightRequest + scalingFactor.Top, 20);
            draggingView.Margin = new Thickness(location.X, location.Y, location.X + draggingView.WidthRequest, location.Y + draggingView.HeightRequest);
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
