using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MAUIDesigner.Fresh.App.Hosting;

public sealed class NamedPipeHostedDesignerBridge : IHostedDesignerBridge
{
    private const string PipeArgument = "--designer-pipe";
    private readonly string? _pipeName;
    private readonly ConcurrentQueue<string> _outgoing = new();
    private readonly SemaphoreSlim _outgoingSignal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _documentChangeGate = new();
    private BridgeMessage? _unacknowledgedDocumentChange;
    private long _documentRevision;
    private int _started;

    public NamedPipeHostedDesignerBridge(string? pipeName = null)
    {
        if (!string.IsNullOrWhiteSpace(pipeName))
        {
            _pipeName = pipeName;
            return;
        }

        string[] arguments = Environment.GetCommandLineArgs();
        int pipeIndex = Array.IndexOf(arguments, PipeArgument);
        if (pipeIndex >= 0 && pipeIndex + 1 < arguments.Length)
        {
            _pipeName = arguments[pipeIndex + 1];
        }
    }

    public bool IsHosted => !string.IsNullOrWhiteSpace(_pipeName);

    public event EventHandler<string>? DocumentLoadRequested;

    public event EventHandler<string>? CloseRequested;

    public event EventHandler<string>? ErrorReported;

    public event EventHandler<HostedDesignerCommand>? CommandRequested;

    public void Start()
    {
        if (!IsHosted || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _ = Task.Run(RunAsync);
    }

    public void SendDocumentChanged(string xaml)
    {
        var message = new BridgeMessage(
            "document.changed",
            xaml,
            Revision: Interlocked.Increment(ref _documentRevision));
        lock (_documentChangeGate)
        {
            _unacknowledgedDocumentChange = message;
        }

        Enqueue(message);
    }

    public void SendTextInputFocusChanged(bool textInputFocused) =>
        Enqueue(new BridgeMessage(
            "designer.focusChanged",
            TextInputFocused: textInputFocused));

    public void SendToolboxSnapshot(IReadOnlyList<HostedToolboxItem> items) =>
        Enqueue(new BridgeMessage(
            "designer.toolboxChanged",
            ToolboxItems: items));

    public void SendSelectionSnapshot(HostedSelectionSnapshot selection) =>
        Enqueue(new BridgeMessage(
            "designer.selectionChanged",
            Selection: selection));

    public void SendClosed(string requestId, string? xaml) =>
        Enqueue(new BridgeMessage("designer.closed", xaml, requestId));

    private async Task RunAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync();
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (
                exception is IOException or TimeoutException or JsonException)
            {
                ErrorReported?.Invoke(this, $"Visual Studio connection failed: {exception.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _shutdown.Token);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RunConnectionAsync()
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            _pipeName!,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync(15_000, _shutdown.Token);
        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe);
        using var connectionLifetime =
            CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);

        await writer.WriteLineAsync(JsonSerializer.Serialize(new BridgeMessage("designer.ready")));
        await writer.FlushAsync(connectionLifetime.Token);
        Task read = ReadMessagesAsync(reader, connectionLifetime.Token);
        Task write = WriteMessagesAsync(writer, connectionLifetime.Token);
        Task retry = RetryDocumentChangesAsync(connectionLifetime.Token);
        Task completed = await Task.WhenAny(read, write, retry);
        connectionLifetime.Cancel();
        try
        {
            await Task.WhenAll(read, write, retry);
        }
        catch (OperationCanceledException) when (connectionLifetime.IsCancellationRequested)
        {
        }

        await completed;
    }

    private async Task ReadMessagesAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? json = await reader.ReadLineAsync(cancellationToken);
            if (json is null)
            {
                throw new IOException("Visual Studio closed the designer connection.");
            }

            BridgeMessage? message = JsonSerializer.Deserialize<BridgeMessage>(json);
            if (message?.Type == "document.load" && message.Xaml is not null)
            {
                lock (_documentChangeGate)
                {
                    _unacknowledgedDocumentChange = null;
                }

                DocumentLoadRequested?.Invoke(this, message.Xaml);
            }
            else if (message?.Type == "document.applied" &&
                     message.Revision is long revision)
            {
                lock (_documentChangeGate)
                {
                    if (_unacknowledgedDocumentChange?.Revision <= revision)
                    {
                        _unacknowledgedDocumentChange = null;
                    }
                }
            }
            else if (message?.Type == "host.close" &&
                     !string.IsNullOrWhiteSpace(message.RequestId))
            {
                CloseRequested?.Invoke(this, message.RequestId);
            }
            else if (message?.Type == "host.command" &&
                     !string.IsNullOrWhiteSpace(message.Command))
            {
                CommandRequested?.Invoke(
                    this,
                    new HostedDesignerCommand(
                        message.Command,
                        message.ControlType,
                        message.ElementId,
                        message.PropertyName,
                        message.Value));
            }
        }
    }

    private async Task WriteMessagesAsync(
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _outgoingSignal.WaitAsync(cancellationToken);
            while (_outgoing.TryDequeue(out string? json))
            {
                await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
            }
        }
    }

    private async Task RetryDocumentChangesAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            BridgeMessage? pending;
            lock (_documentChangeGate)
            {
                pending = _unacknowledgedDocumentChange;
            }

            if (pending is not null)
            {
                Enqueue(pending);
            }
        }
    }

    private void Enqueue(BridgeMessage message)
    {
        if (!IsHosted || _shutdown.IsCancellationRequested)
        {
            return;
        }

        _outgoing.Enqueue(JsonSerializer.Serialize(message));
        _outgoingSignal.Release();
    }

    public void Dispose()
    {
        _shutdown.Cancel();
    }

    private sealed record BridgeMessage(
        [property: JsonPropertyName("type")]
        string Type,
        [property: JsonPropertyName("xaml")]
        string? Xaml = null,
        [property: JsonPropertyName("requestId")]
        string? RequestId = null,
        [property: JsonPropertyName("revision")]
        long? Revision = null,
        [property: JsonPropertyName("command")]
        string? Command = null,
        [property: JsonPropertyName("textInputFocused")]
        bool? TextInputFocused = null,
        [property: JsonPropertyName("toolboxItems")]
        IReadOnlyList<HostedToolboxItem>? ToolboxItems = null,
        [property: JsonPropertyName("selection")]
        HostedSelectionSnapshot? Selection = null,
        [property: JsonPropertyName("controlType")]
        string? ControlType = null,
        [property: JsonPropertyName("elementId")]
        string? ElementId = null,
        [property: JsonPropertyName("propertyName")]
        string? PropertyName = null,
        [property: JsonPropertyName("value")]
        string? Value = null);
}
