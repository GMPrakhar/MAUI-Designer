using MAUIDesigner.HelperViews;
using MAUIDesigner.LayoutDesigners;
using static MAUIDesigner.DnDHelper.ScalingHelper;

namespace MAUIDesigner.DnDHelper
{
    internal static class DragAndDropOperations
    {
        public static Action<object> OnFocusChanged;

        public static AbsoluteLayout BaseLayout;

        public static void OnDrop(object sender, DropEventArgs e)
        {
            e.Data.Properties.TryGetValue("IsScaling", out object IsScalingObject);
            e.Data.Properties.TryGetValue("DraggingView", out object draggingObject);
            e.Data.Properties.TryGetValue("DragLocation", out object dragLocation);
            var draggingView = draggingObject as View;
            var dragLocationInsideView = dragLocation is not null ? (Point)dragLocation : Point.Zero;

            var parentView = (sender as GestureRecognizer).Parent;
            if (parentView is ElementDesignerView designerView)
            {
                parentView = designerView.View;
            }

            if (parentView == (draggingView?.Parent as ElementDesignerView)?.View) return;

            var location = e.GetPosition(draggingView).Value;

            if (IsScalingObject != null && (bool)IsScalingObject == true)
            {
                e.Data.Properties.TryGetValue("ScaleDirection", out object scaleDirectionObject);
                var scaleDirection = (ScaleDirection)scaleDirectionObject;
                ScaleView(draggingView, location, scaleDirection);
            }
            else
            {
                if (draggingObject is not null)
                {
                    var layoutDesigner = LayoutDesignerFactory.CreateLayoutDesigner(parentView as Layout);
                    layoutDesigner.OnDrop(draggingView, location.Offset(-dragLocationInsideView.X, -dragLocationInsideView.Y));
                }
            }
        }

        private static Layout GetLayoutAtPosition(Point location)
        {
            return BaseLayout.GetVisualTreeDescendants().Last(x => x is Layout lay && lay.Frame.Contains(location)) as Layout;
        }
    }
}
