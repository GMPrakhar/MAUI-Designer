namespace MAUIDesigner.Fresh.App.Hosting;

public interface IHostedDesignerBridge : IDisposable
{
    bool IsHosted { get; }

    event EventHandler<string>? DocumentLoadRequested;

    event EventHandler<string>? CloseRequested;

    event EventHandler<string>? ErrorReported;

    event EventHandler<HostedDesignerCommand>? CommandRequested;

    event EventHandler<HostedProjectControls>? ProjectControlsReceived;

    void Start();

    void SendDocumentChanged(string xaml);

    void SendTextInputFocusChanged(bool textInputFocused);

    void SendToolboxSnapshot(IReadOnlyList<HostedToolboxItem> items);

    void SendSelectionSnapshot(HostedSelectionSnapshot selection);

    void SendHierarchySnapshot(IReadOnlyList<HostedHierarchyItem> items);

    void SendClosed(string requestId, string? xaml);
}
