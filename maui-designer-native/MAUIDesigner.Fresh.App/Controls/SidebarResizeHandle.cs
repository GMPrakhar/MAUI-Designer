namespace MAUIDesigner.Fresh.App.Controls;

public sealed class SidebarResizeHandle : Border
{
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _platformView;
    private Microsoft.UI.Xaml.UIElement? _captureTarget;
    private Windows.Foundation.Point _start;
    private bool _dragging;
#endif

    public event EventHandler<SidebarResizeEventArgs>? ResizeRequested;

    public event EventHandler? ToggleRequested;

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
                Microsoft.UI.Xaml.UIElement.DoubleTappedEvent,
                new Microsoft.UI.Xaml.Input.DoubleTappedEventHandler(OnDoubleTapped),
                true);
        }
#endif
    }

#if WINDOWS
    private void OnPointerPressed(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_platformView is null ||
            !e.GetCurrentPoint(_platformView).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _start = e.GetCurrentPoint(null).Position;
        _captureTarget = e.OriginalSource as Microsoft.UI.Xaml.UIElement ?? _platformView;
        _dragging = _captureTarget.CapturePointer(e.Pointer);
        if (_dragging)
        {
            e.Handled = true;
            ResizeRequested?.Invoke(this, new SidebarResizeEventArgs(GestureStatus.Started, 0));
        }
    }

    private void OnPointerMoved(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging || _platformView is null)
        {
            return;
        }

        double totalX = e.GetCurrentPoint(null).Position.X - _start.X;
        e.Handled = true;
        ResizeRequested?.Invoke(
            this,
            new SidebarResizeEventArgs(GestureStatus.Running, totalX));
    }

    private void OnPointerReleased(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging || _platformView is null)
        {
            return;
        }

        _dragging = false;
        _captureTarget?.ReleasePointerCapture(e.Pointer);
        _captureTarget = null;
        e.Handled = true;
        ResizeRequested?.Invoke(
            this,
            new SidebarResizeEventArgs(GestureStatus.Completed, 0));
    }

    private void OnPointerCaptureLost(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _captureTarget = null;
        ResizeRequested?.Invoke(
            this,
            new SidebarResizeEventArgs(GestureStatus.Canceled, 0));
    }

    private void OnDoubleTapped(
        object sender,
        Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        ToggleRequested?.Invoke(this, EventArgs.Empty);
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
            Microsoft.UI.Xaml.UIElement.DoubleTappedEvent,
            new Microsoft.UI.Xaml.Input.DoubleTappedEventHandler(OnDoubleTapped));
        _platformView = null;
        _captureTarget = null;
        _dragging = false;
    }
#endif
}

public sealed record SidebarResizeEventArgs(
    GestureStatus StatusType,
    double TotalX);
