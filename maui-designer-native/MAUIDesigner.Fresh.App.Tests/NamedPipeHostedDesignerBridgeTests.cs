using System.IO.Pipes;
using System.Text.Json;
using MAUIDesigner.Fresh.App.Hosting;

namespace MAUIDesigner.Fresh.App.Tests;

public sealed class NamedPipeHostedDesignerBridgeTests
{
    [Fact]
    public async Task Bridge_exchanges_ready_load_and_changed_messages()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        var loaded = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.DocumentLoadRequested += (_, xaml) => loaded.TrySetResult(xaml);

        bridge.Start();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        Assert.Equal("designer.ready", MessageType(await ReadLineAsync(reader)));

        const string loadedXaml = "<Grid />";
        await writer.WriteLineAsync(JsonSerializer.Serialize(new
        {
            type = "document.load",
            xaml = loadedXaml
        }));
        Assert.Equal(
            loadedXaml,
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        const string changedXaml = "<Grid><Label /></Grid>";
        bridge.SendDocumentChanged(changedXaml);
        string changedJson = await ReadLineAsync(reader);
        using JsonDocument changed = JsonDocument.Parse(changedJson);
        Assert.Equal("document.changed", changed.RootElement.GetProperty("type").GetString());
        Assert.Equal(changedXaml, changed.RootElement.GetProperty("xaml").GetString());
    }

    [Fact]
    public async Task Bridge_reports_when_the_host_disconnects()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        var error = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.ErrorReported += (_, message) => error.TrySetResult(message);

        bridge.Start();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using (var reader = new StreamReader(server, leaveOpen: true))
        {
            Assert.Equal("designer.ready", MessageType(await ReadLineAsync(reader)));
        }

        server.Dispose();

        string message = await error.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("closed the designer connection", message);
    }

    [Fact]
    public async Task Close_handshake_returns_the_final_document()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        bridge.CloseRequested += (_, requestId) => bridge.SendClosed(requestId, "<Grid />");

        bridge.Start();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };
        Assert.Equal("designer.ready", MessageType(await ReadLineAsync(reader)));

        const string requestId = "close-2";
        await writer.WriteLineAsync(
            $$"""{"type":"host.close","requestId":"{{requestId}}"}""");

        string response = await ReadLineAsync(reader);
        using JsonDocument message = JsonDocument.Parse(response);
        Assert.Equal("designer.closed", message.RootElement.GetProperty("type").GetString());
        Assert.Equal("<Grid />", message.RootElement.GetProperty("xaml").GetString());
        Assert.Equal(requestId, message.RootElement.GetProperty("requestId").GetString());
    }

    private static async Task<string> ReadLineAsync(StreamReader reader) =>
        await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) ??
        throw new EndOfStreamException();

    private static string? MessageType(string json)
    {
        using JsonDocument message = JsonDocument.Parse(json);
        return message.RootElement.GetProperty("type").GetString();
    }
}
