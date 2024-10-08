using static MAUIDesigner.DnDHelper.ScalingHelper;

namespace MAUIDesigner.DnDHelper
{
    internal static class DragAndDropOperations
    {
        public static Action<object> OnFocusChanged;

        public static void OnDrop(object sender, DropEventArgs e)
        {
            e.Data.Properties.TryGetValue("IsScaling", out object IsScalingObject);
            e.Data.Properties.TryGetValue("DraggingView", out object draggingObject);
            var draggingView = draggingObject as View;

            var parentView = (sender as GestureRecognizer).Parent;

            var location = e.GetPosition((sender as GestureRecognizer).Parent).Value;

            if (IsScalingObject != null && (bool)IsScalingObject == true)
            {
                e.Data.Properties.TryGetValue("ScaleDirection", out object scaleDirectionObject);
                var scaleDirection = (ScaleDirection)scaleDirectionObject;
                ScaleView(draggingView, location, scaleDirection);
            }
            else
            {
                if (draggingObject != null)
                {
                    draggingView.Margin = new Thickness(location.X, location.Y, location.X + draggingView.Width, location.Y + draggingView.Height);
                }
            }
        }
    }
}
