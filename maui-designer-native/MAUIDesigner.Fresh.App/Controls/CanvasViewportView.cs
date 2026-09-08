namespace MAUIDesigner.Fresh.App.Controls;

public sealed class CanvasViewportView : Grid
{
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _platformView;
    private Windows.Foundation.Point _lastPoint;
    private Windows.Foundation.Point _marqueeStartLocal;
    private Windows.Foundation.Point _marqueeStartWindow;
    private bool _isPanning;
    private bool _isMarqueeSelecting;
    private bool _marqueeAdditive;
#endif

    public event EventHandler<CanvasPanEventArgs>? PanRequested;

    public event EventHandler<CanvasZoomEventArgs>? ZoomRequested;

    public event EventHandler<CanvasMarqueeEventArgs>? MarqueeRequested;

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
            if (point.Properties.IsLeftButtonPressed &&
                !IsDesignerChromeSource(e.OriginalSource))
            {
                _marqueeStartLocal = point.Position;
                _marqueeStartWindow = e.GetCurrentPoint(null).Position;
                _marqueeAdditive = (GetKeyState(0x11) & 0x8000) != 0;
                _isMarqueeSelecting = _platformView.CapturePointer(e.Pointer);
                if (_isMarqueeSelecting)
                {
                    MarqueeRequested?.Invoke(
                        this,
                        CreateMarqueeEventArgs(GestureStatus.Started, e));
                    e.Handled = true;
                }
            }

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
            if (_isMarqueeSelecting && _platformView is not null)
            {
                MarqueeRequested?.Invoke(
                    this,
                    CreateMarqueeEventArgs(GestureStatus.Running, e));
                e.Handled = true;
            }

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
        if (_platformView is null)
        {
            return;
        }

        if (_isPanning)
        {
            _isPanning = false;
            _platformView.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
        else if (_isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;
            MarqueeRequested?.Invoke(
                this,
                CreateMarqueeEventArgs(GestureStatus.Completed, e));
            _platformView.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnPointerCaptureLost(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _isPanning = false;
        if (_isMarqueeSelecting)
        {
            _isMarqueeSelecting = false;
            MarqueeRequested?.Invoke(
                this,
                new CanvasMarqueeEventArgs(
                    GestureStatus.Canceled,
                    _marqueeStartLocal.X,
                    _marqueeStartLocal.Y,
                    _marqueeStartLocal.X,
                    _marqueeStartLocal.Y,
                    _marqueeStartWindow.X,
                    _marqueeStartWindow.Y,
                    _marqueeStartWindow.X,
                    _marqueeStartWindow.Y,
                    _marqueeAdditive));
        }
    }

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
        _isMarqueeSelecting = false;
    }

    private CanvasMarqueeEventArgs CreateMarqueeEventArgs(
        GestureStatus status,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Windows.Foundation.Point local = e.GetCurrentPoint(_platformView).Position;
        Windows.Foundation.Point window = e.GetCurrentPoint(null).Position;
        return new CanvasMarqueeEventArgs(
            status,
            _marqueeStartLocal.X,
            _marqueeStartLocal.Y,
            local.X,
            local.Y,
            _marqueeStartWindow.X,
            _marqueeStartWindow.Y,
            window.X,
            window.Y,
            _marqueeAdditive);
    }

    private bool IsDesignerChromeSource(object? source)
    {
        for (Microsoft.UI.Xaml.DependencyObject? current =
                 source as Microsoft.UI.Xaml.DependencyObject;
             current is not null && current != _platformView;
             current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is Microsoft.UI.Xaml.FrameworkElement element &&
                Microsoft.UI.Xaml.Automation.AutomationProperties
                    .GetAutomationId(element)
                    .StartsWith("chrome-", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
#endif
}

public sealed record CanvasPanEventArgs(double DeltaX, double DeltaY);

public sealed record CanvasZoomEventArgs(int WheelDelta, double X, double Y);

public sealed record CanvasMarqueeEventArgs(
    GestureStatus StatusType,
    double StartX,
    double StartY,
    double CurrentX,
    double CurrentY,
    double WindowStartX,
    double WindowStartY,
    double WindowCurrentX,
    double WindowCurrentY,
    bool Additive);
