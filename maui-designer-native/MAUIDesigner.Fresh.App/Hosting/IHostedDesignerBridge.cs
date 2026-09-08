namespace MAUIDesigner.Fresh.App.Hosting;

public interface IHostedDesignerBridge : IDisposable
{
    bool IsHosted { get; }

    event EventHandler<string>? DocumentLoadRequested;

    event EventHandler<string>? CloseRequested;

    event EventHandler<string>? ErrorReported;

    event EventHandler<string>? CommandRequested;

    void Start();

    void SendDocumentChanged(string xaml);

    void SendTextInputFocusChanged(bool textInputFocused);

    void SendClosed(string requestId, string? xaml);
}
