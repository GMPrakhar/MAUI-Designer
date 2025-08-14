using Inputs = Microsoft.UI.Input;
using Xamls = Microsoft.UI.Xaml;

namespace MAUIDesigner.Services
{
    public interface ICursorService
    {
        void SetCursor(View view, CursorType cursorType);
        void SetResizeCursor(View view);
        void SetDefaultCursor(View view);
    }

    public enum CursorType
    {
        Arrow,
        Hand,
        SizeWE,
        SizeNS
    }

    public class CursorService : ICursorService
    {
        public void SetCursor(View view, CursorType cursorType)
        {
#if WINDOWS
            if (view.Handler?.PlatformView is Xamls.UIElement element)
            {
                var shape = cursorType switch
                {
                    CursorType.SizeWE => Inputs.InputSystemCursorShape.SizeWestEast,
                    CursorType.SizeNS => Inputs.InputSystemCursorShape.SizeNorthSouth,
                    CursorType.Hand => Inputs.InputSystemCursorShape.Hand,
                    _ => Inputs.InputSystemCursorShape.Arrow
                };
                element.ChangeCursor(Inputs.InputSystemCursor.Create(shape));
            }
#endif
        }

        public void SetResizeCursor(View view)
        {
            // Determine cursor type based on view name or type
            if (view is Rectangle rect)
            {
                var cursorType = rect.StyleId switch
                {
                    "TabDraggerLeft" or "TabDraggerRight" => CursorType.SizeWE,
                    "TabDraggerBottom" => CursorType.SizeNS,
                    _ => CursorType.Arrow
                };
                SetCursor(view, cursorType);
            }
        }

        public void SetDefaultCursor(View view)
        {
            SetCursor(view, CursorType.Arrow);
        }
    }
}