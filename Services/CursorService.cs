using Inputs = Microsoft.UI.Input;
using Xamls = Microsoft.UI.Xaml;

namespace MAUIDesigner.Services
{
    public interface ICursorService
    {
        void SetCursor(View view, CursorType cursorType);
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
    }
}