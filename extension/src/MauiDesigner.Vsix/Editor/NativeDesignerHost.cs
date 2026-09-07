using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace MauiDesigner.Vsix
{
    /// <summary>Reparents the native designer window into a WPF editor surface.</summary>
    internal sealed class NativeDesignerHost : HwndHost
    {
        private const int GwlStyle = -16;
        private const long WsChild = 0x40000000L;
        private const long WsPopup = 0x80000000L;
        private const long WsCaption = 0x00C00000L;
        private const long WsThickFrame = 0x00040000L;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private readonly TaskCompletionSource<IntPtr> _handleReady =
            new TaskCompletionSource<IntPtr>(TaskCreationOptions.RunContinuationsAsynchronously);
        private IntPtr _hostHandle;
        private IntPtr _designerHandle;

        // BuildWindowCore is owned by WPF, so this completion source is the
        // bridge into that lifecycle rather than independently scheduled work.
#pragma warning disable VSTHRD003
        public Task<IntPtr> WaitForHandleAsync() => _handleReady.Task;
#pragma warning restore VSTHRD003

        public void Attach(IntPtr designerHandle)
        {
            if (_hostHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("The Visual Studio host window is not ready.");
            }

            long style = GetWindowLongPtr(designerHandle, GwlStyle).ToInt64();
            style = (style & ~(WsPopup | WsCaption | WsThickFrame)) | WsChild;
            SetWindowLongPtr(designerHandle, GwlStyle, new IntPtr(style));
            if (SetParent(designerHandle, _hostHandle) == IntPtr.Zero &&
                Marshal.GetLastWin32Error() != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _designerHandle = designerHandle;
            ResizeDesigner();
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostHandle = CreateWindowEx(
                0,
                "static",
                string.Empty,
                unchecked((int)(WsChild | 0x10000000L)),
                0,
                0,
                1,
                1,
                hwndParent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
            if (_hostHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            _handleReady.TrySetResult(_hostHandle);
            return new HandleRef(this, _hostHandle);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            if (_designerHandle != IntPtr.Zero)
            {
                SetParent(_designerHandle, IntPtr.Zero);
                _designerHandle = IntPtr.Zero;
            }

            DestroyWindow(hwnd.Handle);
            _hostHandle = IntPtr.Zero;
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            ResizeDesigner();
        }

        private void ResizeDesigner()
        {
            if (_hostHandle == IntPtr.Zero ||
                _designerHandle == IntPtr.Zero ||
                !GetClientRect(_hostHandle, out NativeRect bounds))
            {
                return;
            }

            SetWindowPos(
                _designerHandle,
                IntPtr.Zero,
                0,
                0,
                bounds.Right - bounds.Left,
                bounds.Bottom - bounds.Top,
                SwpNoActivate | SwpFrameChanged | SwpShowWindow);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int extendedStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr window, out NativeRect bounds);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
