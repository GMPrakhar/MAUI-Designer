using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;
using MAUIDesigner.Services;

namespace MAUIDesigner
{
    internal static class ElementOperations
    {
        private static readonly ICursorService _cursorService = new CursorService();
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating element in designer frame: {ex.Message}");
            }
        }

        internal static void AddDesignerGestureControls(ElementDesignerView newElement)
        {
            var rightClickRecognizer = new TapGestureRecognizer();
            rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
            rightClickRecognizer.Buttons = ButtonsMask.Secondary;

            // Cursor changes to pointer on an Element
            var pointerGestureRecognizer = new PointerGestureRecognizer();
            pointerGestureRecognizer.PointerEntered += (s, e) => _cursorService.SetCursor(s as View, CursorType.Hand);
            pointerGestureRecognizer.PointerExited += (s, e) => _cursorService.SetCursor(s as View, CursorType.Arrow);

            if(newElement.View is Layout)
            {
                var dropGestureRecognizer = new DropGestureRecognizer();
                dropGestureRecognizer.Drop += DragAndDropOperations.OnDrop;
                newElement.GestureRecognizers.Add(dropGestureRecognizer);
            }


            newElement.GestureRecognizers.Add(rightClickRecognizer);
            newElement.GestureRecognizers.Add(pointerGestureRecognizer);
        }
    }
}
