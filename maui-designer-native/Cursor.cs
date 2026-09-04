using System.Runtime.InteropServices;

namespace MAUIDesigner
{
    internal static class Cursor
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator Point(POINT point) => new(point.X, point.Y);
        }

#if WINDOWS
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(ushort virtualKeyCode);

        public static Point GetCursorPosition()
        {
            GetCursorPos(out var lpPoint);
            return lpPoint;
        }

        public static bool IsMousePressed()
        {
            return (GetAsyncKeyState(0x01) & 0x8000) != 0;
        }
#else
        public static Point GetCursorPosition() => Point.Zero;
        public static bool IsMousePressed() => true;
#endif
    }
}
