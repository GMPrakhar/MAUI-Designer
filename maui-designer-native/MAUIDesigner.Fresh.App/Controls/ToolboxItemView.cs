using MAUIDesigner.Fresh.App.Catalog;

namespace MAUIDesigner.Fresh.App.Controls;

public sealed class ToolboxItemView : Border
{
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _platformView;
    private Windows.Foundation.Point _pointerStart;
    private bool _pointerMoved;
    private bool _pointerPressed;
#endif

    public static readonly BindableProperty DescriptorProperty = BindableProperty.Create(
        nameof(Descriptor),
        typeof(ControlDescriptor),
        typeof(ToolboxItemView));

    public event EventHandler? ItemTapped;

    public event EventHandler<ToolboxDragEventArgs>? ItemDragUpdated;

    public ControlDescriptor? Descriptor
    {
        get => (ControlDescriptor?)GetValue(DescriptorProperty);
        set => SetValue(DescriptorProperty, value);
    }

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
            platformView.PointerPressed += OnPointerPressed;
            platformView.PointerMoved += OnPointerMoved;
            platformView.PointerReleased += OnPointerReleased;
            platformView.PointerCaptureLost += OnPointerCaptureLost;
            platformView.KeyDown += OnKeyDown;
            platformView.IsTabStop = true;
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

        _pointerStart = e.GetCurrentPoint(null).Position;
        _pointerMoved = false;
        _pointerPressed = _platformView.CapturePointer(e.Pointer);
        if (_pointerPressed)
        {
            e.Handled = true;
            ItemDragUpdated?.Invoke(
                this,
                new ToolboxDragEventArgs(GestureStatus.Started, 0, 0));
        }
    }

    private void OnPointerMoved(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_pointerPressed || _platformView is null)
        {
            return;
        }

        Windows.Foundation.Point current = e.GetCurrentPoint(null).Position;
        double totalX = current.X - _pointerStart.X;
        double totalY = current.Y - _pointerStart.Y;
        _pointerMoved |= Math.Abs(totalX) >= 4 || Math.Abs(totalY) >= 4;
        if (!_pointerMoved)
        {
            return;
        }

        e.Handled = true;
        ItemDragUpdated?.Invoke(
            this,
            new ToolboxDragEventArgs(GestureStatus.Running, totalX, totalY));
    }

    private void OnPointerReleased(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_pointerPressed || _platformView is null)
        {
            return;
        }

        _pointerPressed = false;
        _platformView.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
        if (_pointerMoved)
        {
            ItemDragUpdated?.Invoke(
                this,
                new ToolboxDragEventArgs(GestureStatus.Completed, 0, 0));
        }
        else
        {
            ItemDragUpdated?.Invoke(
                this,
                new ToolboxDragEventArgs(GestureStatus.Canceled, 0, 0));
            ItemTapped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPointerCaptureLost(
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_pointerPressed)
        {
            return;
        }

        _pointerPressed = false;
        ItemDragUpdated?.Invoke(
            this,
            new ToolboxDragEventArgs(GestureStatus.Canceled, 0, 0));
    }

    private void OnKeyDown(
        object sender,
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
        {
            e.Handled = true;
            ItemTapped?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DetachPlatformView()
    {
        if (_platformView is null)
        {
            return;
        }

        _platformView.PointerPressed -= OnPointerPressed;
        _platformView.PointerMoved -= OnPointerMoved;
        _platformView.PointerReleased -= OnPointerReleased;
        _platformView.PointerCaptureLost -= OnPointerCaptureLost;
        _platformView.KeyDown -= OnKeyDown;
        _platformView = null;
        _pointerPressed = false;
    }
#endif
}

public sealed record ToolboxDragEventArgs(
    GestureStatus StatusType,
    double TotalX,
    double TotalY);
