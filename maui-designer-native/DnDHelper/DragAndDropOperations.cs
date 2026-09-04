using MAUIDesigner.HelperViews;
using MAUIDesigner.LayoutDesigners;
using static MAUIDesigner.DnDHelper.ScalingHelper;

namespace MAUIDesigner.DnDHelper
{
    internal static class DragAndDropOperations
    {
        public static Action<object?>? OnFocusChanged;

        public static AbsoluteLayout? BaseLayout;

        public static void OnDrop(object? sender, DropEventArgs e)
        {
            e.Data.Properties.TryGetValue("IsScaling", out var isScalingObject);
            e.Data.Properties.TryGetValue("DraggingView", out var draggingObject);
            e.Data.Properties.TryGetValue("DragLocation", out var dragLocation);
            var draggingView = draggingObject as View;
            var dragLocationInsideView = dragLocation is Point point ? point : Point.Zero;

            var parentView = (sender as GestureRecognizer)?.Parent as VisualElement;
            if (parentView is ElementDesignerView designerView)
            {
                parentView = designerView.View;
            }

            if (parentView == (draggingView?.Parent as ElementDesignerView)?.View)
            {
                return;
            }

            var location = e.GetPosition(parentView as Element);
            if (location is not Point dropPoint)
            {
                return;
            }

            if (isScalingObject is true)
            {
                e.Data.Properties.TryGetValue("ScaleDirection", out var scaleDirectionObject);
                if (scaleDirectionObject is ScaleDirection scaleDirection)
                {
                    ScaleView(draggingView, dropPoint, scaleDirection);
                }
            }
            else if (draggingView is not null && parentView is Layout parentLayout)
            {
                var layoutDesigner = LayoutDesignerFactory.CreateLayoutDesigner(parentLayout);
                layoutDesigner.OnDrop(draggingView, dropPoint.Offset(-dragLocationInsideView.X, -dragLocationInsideView.Y));
            }
        }
    }
}
