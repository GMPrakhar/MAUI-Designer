namespace MAUIDesigner.Fresh.App.Controls;

public sealed class CanvasViewportView : Grid
{
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _platformView;
    private Windows.Foundation.Point _lastPoint;
    private bool _isPanning;
#endif

    public event EventHandler<CanvasPanEventArgs>? PanRequested;

    public event EventHandler<CanvasZoomEventArgs>? ZoomRequested;

    protected override void OnHandlerChanged()
    {
#if WINDOWS
        DetachPlatformView();
#endif
        base.OnHandlerChanged();
#if WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement platformView)
        {
            _platformView = platformView;
            platformView.AddHandler(
                Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerPressed),
                true);
            platformView.AddHandler(
                Microsoft.UI.Xaml.UIElement.PointerMovedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerMoved),
                true);
            platformView.AddHandler(
                Microsoft.UI.Xaml.UIElement.PointerReleasedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerReleased),
                true);
            platformView.AddHandler(
                Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerCaptureLost),
                true);
            platformView.AddHandler(
                Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent,
                new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerWheelChanged),
                true);
        }
#endif
    }

#if WINDOWS
    private void OnPointerPressed(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_platformView is null)
        {
            return;
        }

        Microsoft.UI.Input.PointerPoint point = e.GetCurrentPoint(_platformView);
        bool spaceDrag = point.Properties.IsLeftButtonPressed &&
            (GetKeyState(0x20) & 0x8000) != 0;
        if (!point.Properties.IsMiddleButtonPressed && !spaceDrag)
        {
            return;
        }

        _lastPoint = point.Position;
        _isPanning = _platformView.CapturePointer(e.Pointer);
        e.Handled = _isPanning;
    }

    private void OnPointerMoved(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isPanning || _platformView is null)
        {
            return;
        }

        Windows.Foundation.Point current = e.GetCurrentPoint(_platformView).Position;
        PanRequested?.Invoke(
            this,
            new CanvasPanEventArgs(current.X - _lastPoint.X, current.Y - _lastPoint.Y));
        _lastPoint = current;
        e.Handled = true;
    }

    private void OnPointerReleased(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isPanning || _platformView is null)
        {
            return;
        }

        _isPanning = false;
        _platformView.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) =>
        _isPanning = false;

    private void OnPointerWheelChanged(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_platformView is null || (GetKeyState(0x11) & 0x8000) == 0)
        {
            return;
        }

        Microsoft.UI.Input.PointerPoint point = e.GetCurrentPoint(_platformView);
        ZoomRequested?.Invoke(
            this,
            new CanvasZoomEventArgs(
                point.Properties.MouseWheelDelta,
                point.Position.X,
                point.Position.Y));
        e.Handled = true;
    }

    private void DetachPlatformView()
    {
        if (_platformView is null)
        {
            return;
        }

        _platformView.RemoveHandler(
            Microsoft.UI.Xaml.UIElement.PointerPressedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerPressed));
        _platformView.RemoveHandler(
            Microsoft.UI.Xaml.UIElement.PointerMovedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerMoved));
        _platformView.RemoveHandler(
            Microsoft.UI.Xaml.UIElement.PointerReleasedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerReleased));
        _platformView.RemoveHandler(
            Microsoft.UI.Xaml.UIElement.PointerCaptureLostEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerCaptureLost));
        _platformView.RemoveHandler(
            Microsoft.UI.Xaml.UIElement.PointerWheelChangedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnPointerWheelChanged));
        _platformView = null;
        _isPanning = false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
#endif
}

public sealed record CanvasPanEventArgs(double DeltaX, double DeltaY);

public sealed record CanvasZoomEventArgs(int WheelDelta, double X, double Y);
