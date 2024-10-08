
using Xamls = Microsoft.UI.Xaml;
using Inputs = Microsoft.UI.Input;
using MAUIDesigner.HelperViews;
using MAUIDesigner.DnDHelper;

namespace MAUIDesigner
{
    internal static class ElementOperations
    {
        internal static void CreateElementInDesignerFrame(Layout MainDesignerView, object sender)
        {
            try
            {
                var senderView = sender as HorizontalStackLayout;
                var newElement = ElementCreator.Create((senderView.Children[1] as Label).Text.Trim());
                var border = new ElementDesignerView(newElement);

                AddDesignerGestureControls(border);
                DragAndDropOperations.OnFocusChanged(border);
                MainDesignerView.Add(border);
            }
            catch (Exception et)
            {
                Console.WriteLine(et);
                // Do nothing
            }
        }

        internal static void AddDesignerGestureControls(View newElement)
        {
            var rightClickRecognizer = new TapGestureRecognizer();
            rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
            rightClickRecognizer.Buttons = ButtonsMask.Secondary;

            // Cursor changes to pointer on an Element
            var pointerGestureRecognizer = new PointerGestureRecognizer();
            pointerGestureRecognizer.PointerEntered += (s, e) => ChangeCursorToHand(s);
            pointerGestureRecognizer.PointerExited += (s, e) => ChangeCursorToDefault(s);


            newElement.GestureRecognizers.Add(rightClickRecognizer);
            newElement.GestureRecognizers.Add(pointerGestureRecognizer);
        }

        private static void ChangeCursorToHand(object sender)
        {
            var view = sender as View;
            if (view != null)
            {
                (view.Handler.PlatformView as Xamls.UIElement).ChangeCursor(Inputs.InputSystemCursor.Create(Inputs.InputSystemCursorShape.Hand));
            }
        }

        private static void ChangeCursorToDefault(object sender)
        {
            var view = sender as View;
            if (view != null)
            {
                (view.Handler.PlatformView as Xamls.UIElement).ChangeCursor(Inputs.InputSystemCursor.Create(Inputs.InputSystemCursorShape.Arrow));
            }
        }
    }
}
