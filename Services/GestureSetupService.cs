using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;
using Microsoft.Maui.Controls.Shapes;

namespace MAUIDesigner.Services
{
    public interface IGestureSetupService
    {
        void SetupDesignerGestures(AbsoluteLayout designerFrame, ContextMenu contextMenu, EventHandler<TappedEventArgs> tapHandler);
        void SetupTabDraggers(Rectangle tabDraggerLeft, Rectangle tabDraggerRight, Rectangle tabDraggerBottom, 
                            EventHandler<PanUpdatedEventArgs> panHandler, ICursorService cursorService);
    }

    public class GestureSetupService : IGestureSetupService
    {
        public void SetupDesignerGestures(AbsoluteLayout designerFrame, ContextMenu contextMenu, EventHandler<TappedEventArgs> tapHandler)
        {
            // Setup context menu
            var rightClickRecognizer = new TapGestureRecognizer();
            rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
            rightClickRecognizer.Buttons = ButtonsMask.Secondary;
            designerFrame.GestureRecognizers.Add(rightClickRecognizer);

            // Setup drag and drop
            var dropGestureRecognizer = new DropGestureRecognizer();
            dropGestureRecognizer.Drop += DragAndDropOperations.OnDrop;
            designerFrame.GestureRecognizers.Add(dropGestureRecognizer);

            // Setup tap gesture for hiding context menu
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += tapHandler;
            designerFrame.GestureRecognizers.Add(tapGesture);

            DragAndDropOperations.BaseLayout = designerFrame;
        }

        public void SetupTabDraggers(Rectangle tabDraggerLeft, Rectangle tabDraggerRight, Rectangle tabDraggerBottom, 
                                   EventHandler<PanUpdatedEventArgs> panHandler, ICursorService cursorService)
        {
            // Attach PanGestureRecognizer for TabDragger rectangles
            AttachPanGestureRecognizer(tabDraggerLeft, panHandler);
            AttachPanGestureRecognizer(tabDraggerRight, panHandler);
            AttachPanGestureRecognizer(tabDraggerBottom, panHandler);

            // Attach pointer recognizers for draggers
            AttachPointerGestureRecognizer(tabDraggerLeft, CursorType.SizeWE, cursorService);
            AttachPointerGestureRecognizer(tabDraggerRight, CursorType.SizeWE, cursorService);
            AttachPointerGestureRecognizer(tabDraggerBottom, CursorType.SizeNS, cursorService);
        }

        private void AttachPanGestureRecognizer(Rectangle tabDragger, EventHandler<PanUpdatedEventArgs> panHandler)
        {
            var panGestureRecognizer = new PanGestureRecognizer();
            panGestureRecognizer.PanUpdated += panHandler;
            tabDragger.GestureRecognizers.Add(panGestureRecognizer);
        }

        private void AttachPointerGestureRecognizer(Rectangle tabDragger, CursorType cursorType, ICursorService cursorService)
        {
            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += (s, e) => cursorService.SetCursor(tabDragger, cursorType);
            pointerGesture.PointerExited += (s, e) => cursorService.SetCursor(tabDragger, CursorType.Arrow);
            tabDragger.GestureRecognizers.Add(pointerGesture);
        }
    }
}