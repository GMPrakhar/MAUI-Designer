using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Preview;

public sealed class RuntimePreviewService : IRuntimePreviewService
{
    public const string WindowTitle = "MAUI Designer Preview";

    private readonly PreviewDocumentRenderer _renderer;
    private readonly IPreviewDispatcher _dispatcher;
    private readonly IPreviewWindowHost _windowHost;

    public RuntimePreviewService(
        PreviewDocumentRenderer renderer,
        IPreviewDispatcher dispatcher,
        IPreviewWindowHost windowHost)
    {
        _renderer = renderer;
        _dispatcher = dispatcher;
        _windowHost = windowHost;
    }

    public Task<Window> OpenAsync(
        DesignerDocument document,
        RuntimePreviewOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= RuntimePreviewOptions.Responsive;

        return _dispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            View renderedContent = _renderer.Materialize(document);
            Window? window = null;
            var closeButton = new Button
            {
                Text = "Close",
                AutomationId = "preview-close",
                Padding = new Thickness(14, 6),
                Command = new Command(() =>
                {
                    if (window is not null)
                    {
                        _windowHost.Close(window);
                    }
                })
            };
            var header = new Grid
            {
                AutomationId = "preview-header",
                Padding = new Thickness(16, 10),
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            header.Add(new Label
            {
                Text = options.DisplayText,
                AutomationId = "preview-device-info",
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#334155"),
                VerticalTextAlignment = TextAlignment.Center
            });
            header.Add(closeButton, 1);

            var deviceSurface = new Border
            {
                AutomationId = "preview-device-surface",
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#CBD5E1"),
                StrokeThickness = 1,
                Content = renderedContent,
                WidthRequest = options.Width > 0 ? options.Width : -1,
                HeightRequest = options.Height > 0 ? options.Height : -1,
                HorizontalOptions = options.Width > 0
                    ? LayoutOptions.Center
                    : LayoutOptions.Fill,
                VerticalOptions = options.Height > 0
                    ? LayoutOptions.Start
                    : LayoutOptions.Fill
            };
            var pageLayout = new Grid
            {
                BackgroundColor = Color.FromArgb("#E2E8F0"),
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Star)
                }
            };
            pageLayout.Add(header);
            pageLayout.Add(new ScrollView
            {
                AutomationId = "preview-scroll",
                Orientation = ScrollOrientation.Both,
                Padding = 20,
                Content = deviceSurface
            }, 0, 1);

            window = new Window(new ContentPage
            {
                BackgroundColor = Color.FromArgb("#E2E8F0"),
                Content = pageLayout
            })
            {
                Title = WindowTitle
            };
            _windowHost.Open(window);
            return window;
        });
    }
}

public interface IPreviewDispatcher
{
    Task<T> InvokeAsync<T>(Func<T> action);
}

public sealed class MainThreadPreviewDispatcher : IPreviewDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return MainThread.IsMainThread
            ? Task.FromResult(action())
            : MainThread.InvokeOnMainThreadAsync(action);
    }
}

public interface IPreviewWindowHost
{
    void Open(Window window);

    void Close(Window window);
}

public sealed class MauiPreviewWindowHost : IPreviewWindowHost
{
    public void Open(Window window) =>
        GetApplication().OpenWindow(window);

    public void Close(Window window) =>
        GetApplication().CloseWindow(window);

    private static Application GetApplication() =>
        Application.Current
        ?? throw new InvalidOperationException("The MAUI application is not available.");
}
