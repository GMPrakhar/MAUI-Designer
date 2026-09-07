namespace MAUIDesigner.Fresh.App.Controls;

public sealed class SidebarResizeHandle : View
{
    public event EventHandler<SidebarResizeEventArgs>? ResizeRequested;

    public event EventHandler? ToggleRequested;

    internal void RaiseResizeRequested(GestureStatus statusType, double totalX) =>
        ResizeRequested?.Invoke(this, new SidebarResizeEventArgs(statusType, totalX));

    internal void RaiseToggleRequested() =>
        ToggleRequested?.Invoke(this, EventArgs.Empty);
}

#if WINDOWS
public sealed class SidebarResizeHandleHandler
    : Microsoft.Maui.Handlers.ViewHandler<
        SidebarResizeHandle,
        Microsoft.UI.Xaml.Controls.Primitives.Thumb>
{
    public static readonly IPropertyMapper<SidebarResizeHandle, SidebarResizeHandleHandler>
        Mapper = new PropertyMapper<SidebarResizeHandle, SidebarResizeHandleHandler>(
            Microsoft.Maui.Handlers.ViewHandler.ViewMapper);

    private double _totalX;

    public SidebarResizeHandleHandler()
        : base(Mapper)
    {
    }

    protected override Microsoft.UI.Xaml.Controls.Primitives.Thumb CreatePlatformView() =>
        new()
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 241, 245, 249)),
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            IsTabStop = true,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
        };

    protected override void ConnectHandler(
        Microsoft.UI.Xaml.Controls.Primitives.Thumb platformView)
    {
        base.ConnectHandler(platformView);
        platformView.DragStarted += OnDragStarted;
        platformView.DragDelta += OnDragDelta;
        platformView.DragCompleted += OnDragCompleted;
        platformView.DoubleTapped += OnDoubleTapped;
    }

    protected override void DisconnectHandler(
        Microsoft.UI.Xaml.Controls.Primitives.Thumb platformView)
    {
        platformView.DragStarted -= OnDragStarted;
        platformView.DragDelta -= OnDragDelta;
        platformView.DragCompleted -= OnDragCompleted;
        platformView.DoubleTapped -= OnDoubleTapped;
        base.DisconnectHandler(platformView);
    }

    private void OnDragStarted(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.DragStartedEventArgs e)
    {
        _totalX = 0;
        VirtualView.RaiseResizeRequested(GestureStatus.Started, 0);
    }

    private void OnDragDelta(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
    {
        _totalX += e.HorizontalChange;
        VirtualView.RaiseResizeRequested(GestureStatus.Running, _totalX);
    }

    private void OnDragCompleted(
        object sender,
        Microsoft.UI.Xaml.Controls.Primitives.DragCompletedEventArgs e)
    {
        VirtualView.RaiseResizeRequested(
            e.Canceled ? GestureStatus.Canceled : GestureStatus.Completed,
            _totalX);
        _totalX = 0;
    }

    private void OnDoubleTapped(
        object sender,
        Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        VirtualView.RaiseToggleRequested();
    }
}
#endif

public sealed record SidebarResizeEventArgs(
    GestureStatus StatusType,
    double TotalX);
