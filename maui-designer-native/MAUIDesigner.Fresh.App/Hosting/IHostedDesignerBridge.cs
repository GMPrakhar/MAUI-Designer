namespace MAUIDesigner.Fresh.App.Hosting;

public interface IHostedDesignerBridge : IDisposable
{
    bool IsHosted { get; }

    event EventHandler<string>? DocumentLoadRequested;

    event EventHandler<string>? CloseRequested;

    event EventHandler<string>? ErrorReported;

    void Start();

    void SendDocumentChanged(string xaml);

    void SendClosed(string requestId, string? xaml);
}
