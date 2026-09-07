using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Preview;
using MAUIDesigner.Fresh.Core.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class RuntimePreviewTests
{
    [Fact]
    public void Fixed_device_options_have_stable_display_text()
    {
        var options = new RuntimePreviewOptions("Phone", 390, 844);

        Assert.Equal("Phone · 390 × 844", options.DisplayText);
    }

    [Fact]
    public void Responsive_options_do_not_claim_fixed_dimensions()
    {
        Assert.Equal("Responsive", RuntimePreviewOptions.Responsive.DisplayText);
    }

    [Fact]
    public async Task Service_honors_cancellation_before_dispatching()
    {
        ReflectionControlCatalog catalog = CreateCatalog();
        ControlDescriptor label = catalog.Controls.Single(
            control => control.RuntimeType == typeof(Label));
        DesignerDocument document = DesignerDocument.Create(label.Id);
        var dispatcher = new RecordingDispatcher();
        var host = new RecordingWindowHost();
        var service = new RuntimePreviewService(
            new PreviewDocumentRenderer(catalog),
            dispatcher,
            host);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.OpenAsync(document, cancellationToken: cancellation.Token));

        Assert.False(dispatcher.WasInvoked);
        Assert.Null(host.Opened);
    }

    private static ReflectionControlCatalog CreateCatalog()
    {
        var catalog = new ReflectionControlCatalog(
            new ServiceCollection().BuildServiceProvider());
        catalog.RegisterAssembly(typeof(View).Assembly);
        return catalog;
    }

    private sealed class RecordingDispatcher : IPreviewDispatcher
    {
        public bool WasInvoked { get; private set; }

        public Task<T> InvokeAsync<T>(Func<T> action)
        {
            WasInvoked = true;
            return Task.FromResult(action());
        }
    }

    private sealed class RecordingWindowHost : IPreviewWindowHost
    {
        public Window? Opened { get; private set; }

        public void Open(Window window) => Opened = window;

        public void Close(Window window)
        {
        }
    }
}
