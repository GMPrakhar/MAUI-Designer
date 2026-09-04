using MAUIDesigner.HelperViews;

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

            bool isInGrid = draggingView.Parent is Grid || draggingView.Parent?.Parent is Grid;

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
                    newWidth = Math.Max(20, currentWidth + (currentX - location.X));
                    newHeight = Math.Max(20, currentHeight + (currentY - location.Y));
                    newX = currentX - (newWidth - currentWidth);
                    newY = currentY - (newHeight - currentHeight);
                    break;
                case ScaleDirection.TopRight:
                    newWidth = Math.Max(20, location.X - currentX);
                    newHeight = Math.Max(20, currentHeight + (currentY - location.Y));
                    newY = currentY - (newHeight - currentHeight);
                    break;
                case ScaleDirection.BottomLeft:
                    newWidth = Math.Max(20, currentWidth + (currentX - location.X));
                    newHeight = Math.Max(20, location.Y - currentY);
                    newX = currentX - (newWidth - currentWidth);
                    break;
                case ScaleDirection.BottomRight:
                    newWidth = Math.Max(20, location.X - currentX);
                    newHeight = Math.Max(20, location.Y - currentY);
                    break;
                case ScaleDirection.Top:
                    newHeight = Math.Max(20, currentHeight + (currentY - location.Y));
                    newY = currentY - (newHeight - currentHeight);
                    break;
                case ScaleDirection.Bottom:
                    newHeight = Math.Max(20, location.Y - currentY);
                    break;
                case ScaleDirection.Left:
                    newWidth = Math.Max(20, currentWidth + (currentX - location.X));
                    newX = currentX - (newWidth - currentWidth);
                    break;
                case ScaleDirection.Right:
                    newWidth = Math.Max(20, location.X - currentX);
                    break;
            }

            draggingView.WidthRequest = newWidth;
            draggingView.HeightRequest = newHeight;

            if (!isInGrid)
            {
                draggingView.Margin = new Thickness(newX, newY, 0, 0);
            }

            if (draggingView.Parent is ElementDesignerView designerView)
            {
                designerView.RefreshSizeLabel();
            }
        }

        public enum ScaleDirection
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Top,
            Bottom,
            Left,
            Right
        }
    }
}
