using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;

namespace MAUIDesigner.Services
{
    public interface IGestureSetupService
    {
        void SetupDesignerGestures(AbsoluteLayout designerFrame, ContextMenu contextMenu, EventHandler<TappedEventArgs> tapHandler);
        void SetupTabDraggers(View tabDraggerLeft, View tabDraggerRight, View tabDraggerBottom,
                            EventHandler<PanUpdatedEventArgs> panHandler, ICursorService cursorService);
    }

    public class GestureSetupService : IGestureSetupService
    {
        public void SetupDesignerGestures(AbsoluteLayout designerFrame, ContextMenu contextMenu, EventHandler<TappedEventArgs> tapHandler)
        {
            var rightClickRecognizer = new TapGestureRecognizer();
            rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
            rightClickRecognizer.Buttons = ButtonsMask.Secondary;
            designerFrame.GestureRecognizers.Add(rightClickRecognizer);

            var dropGestureRecognizer = new DropGestureRecognizer();
            dropGestureRecognizer.Drop += DragAndDropOperations.OnDrop;
            designerFrame.GestureRecognizers.Add(dropGestureRecognizer);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += tapHandler;
            designerFrame.GestureRecognizers.Add(tapGesture);

            DragAndDropOperations.BaseLayout = designerFrame;
        }

        public void SetupTabDraggers(View tabDraggerLeft, View tabDraggerRight, View tabDraggerBottom,
                                    EventHandler<PanUpdatedEventArgs> panHandler, ICursorService cursorService)
        {
            AttachPanGestureRecognizer(tabDraggerLeft, panHandler);
            AttachPanGestureRecognizer(tabDraggerRight, panHandler);
            AttachPanGestureRecognizer(tabDraggerBottom, panHandler);

            AttachPointerGestureRecognizer(tabDraggerLeft, CursorType.SizeWE, cursorService);
            AttachPointerGestureRecognizer(tabDraggerRight, CursorType.SizeWE, cursorService);
            AttachPointerGestureRecognizer(tabDraggerBottom, CursorType.SizeNS, cursorService);
        }

        private void AttachPanGestureRecognizer(View tabDragger, EventHandler<PanUpdatedEventArgs> panHandler)
        {
            var panGestureRecognizer = new PanGestureRecognizer();
            panGestureRecognizer.PanUpdated += panHandler;
            tabDragger.GestureRecognizers.Add(panGestureRecognizer);
        }

        private void AttachPointerGestureRecognizer(View tabDragger, CursorType cursorType, ICursorService cursorService)
        {
            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += (s, e) => cursorService.SetCursor(tabDragger, cursorType);
            pointerGesture.PointerExited += (s, e) => cursorService.SetCursor(tabDragger, CursorType.Arrow);
            tabDragger.GestureRecognizers.Add(pointerGesture);
        }
    }
}
