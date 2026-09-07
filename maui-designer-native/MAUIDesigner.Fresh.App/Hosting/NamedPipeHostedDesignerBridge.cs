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

    public void Start()
    {
        if (!IsHosted || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        _ = Task.Run(RunAsync);
    }

    public void SendDocumentChanged(string xaml) =>
        Enqueue(new BridgeMessage("document.changed", xaml));

    public void SendClosed(string requestId, string? xaml) =>
        Enqueue(new BridgeMessage("designer.closed", xaml, requestId));

    private async Task RunAsync()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                _pipeName!,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(15_000, _shutdown.Token);
            using var reader = new StreamReader(pipe);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            using var connectionLifetime =
                CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);

            Enqueue(new BridgeMessage("designer.ready"));
            Task read = ReadMessagesAsync(reader, connectionLifetime.Token);
            Task write = WriteMessagesAsync(writer, connectionLifetime.Token);
            await Task.WhenAny(read, write);
            connectionLifetime.Cancel();
            await Task.WhenAll(read, write);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (
            exception is IOException or TimeoutException or JsonException)
        {
            ErrorReported?.Invoke(this, $"Visual Studio connection failed: {exception.Message}");
        }
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
                DocumentLoadRequested?.Invoke(this, message.Xaml);
            }
            else if (message?.Type == "host.close" &&
                     !string.IsNullOrWhiteSpace(message.RequestId))
            {
                CloseRequested?.Invoke(this, message.RequestId);
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
        string? RequestId = null);
}
