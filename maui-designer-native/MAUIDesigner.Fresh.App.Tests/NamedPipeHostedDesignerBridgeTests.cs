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

        await AssertHandshakeAsync(reader);

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
        long revision = changed.RootElement.GetProperty("revision").GetInt64();
        await writer.WriteLineAsync(
            $$"""{"type":"document.applied","revision":{{revision}}}""");
    }

    [Fact]
    public async Task Unacknowledged_document_change_is_retried_until_applied()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        bridge.Start();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };
        await AssertHandshakeAsync(reader);

        bridge.SendDocumentChanged("<Grid><Button /></Grid>");
        using JsonDocument first = JsonDocument.Parse(await ReadLineAsync(reader));
        using JsonDocument retry = JsonDocument.Parse(await ReadLineAsync(reader));
        long revision = first.RootElement.GetProperty("revision").GetInt64();

        Assert.Equal(revision, retry.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal(
            first.RootElement.GetProperty("xaml").GetString(),
            retry.RootElement.GetProperty("xaml").GetString());

        await writer.WriteLineAsync(
            $$"""{"type":"document.applied","revision":{{revision}}}""");
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
            await AssertHandshakeAsync(reader);
        }

        server.Dispose();

        string message = await error.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Visual Studio connection failed", message);
    }

    [Fact]
    public async Task Bridge_reconnects_and_replays_an_unacknowledged_document()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        bridge.Start();

        long revision;
        using (var firstServer = CreateServer(pipeName))
        {
            await firstServer.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
            using var firstReader = new StreamReader(firstServer);
            await AssertHandshakeAsync(firstReader);
            bridge.SendDocumentChanged("<Grid><Label /></Grid>");
            using JsonDocument changed = JsonDocument.Parse(await ReadLineAsync(firstReader));
            revision = changed.RootElement.GetProperty("revision").GetInt64();
        }

        using var secondServer = CreateServer(pipeName);
        await secondServer.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var secondReader = new StreamReader(secondServer);
        using var secondWriter = new StreamWriter(secondServer) { AutoFlush = true };
        await AssertHandshakeAsync(secondReader);
        using JsonDocument replay = JsonDocument.Parse(await ReadLineAsync(secondReader));
        Assert.Equal("document.changed", replay.RootElement.GetProperty("type").GetString());
        Assert.Equal(revision, replay.RootElement.GetProperty("revision").GetInt64());
        Assert.Equal("<Grid><Label /></Grid>", replay.RootElement.GetProperty("xaml").GetString());
        await secondWriter.WriteLineAsync(
            $$"""{"type":"document.applied","revision":{{revision}}}""");
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
        await AssertHandshakeAsync(reader);

        const string requestId = "close-2";
        await writer.WriteLineAsync(
            $$"""{"type":"host.close","requestId":"{{requestId}}"}""");

        string response = await ReadLineAsync(reader);
        using JsonDocument message = JsonDocument.Parse(response);
        Assert.Equal("designer.closed", message.RootElement.GetProperty("type").GetString());
        Assert.Equal("<Grid />", message.RootElement.GetProperty("xaml").GetString());
        Assert.Equal(requestId, message.RootElement.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task Bridge_exchanges_toolbox_selection_and_property_commands()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var server = CreateServer(pipeName);
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        var command = new TaskCompletionSource<HostedDesignerCommand>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.CommandRequested += (_, value) => command.TrySetResult(value);

        bridge.SendToolboxSnapshot(
        [
            new HostedToolboxItem(
                "Microsoft.Maui.Controls.Button",
                "Button",
                "Controls")
        ]);
        bridge.SendSelectionSnapshot(new HostedSelectionSnapshot(
            1,
            "button-1",
            "Button",
            [
                new HostedPropertySnapshot(
                    "Text",
                    "Save",
                    typeof(string).FullName!,
                    "Common",
                    false)
            ]));
        bridge.Start();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };

        await AssertHandshakeAsync(reader);
        using JsonDocument toolbox = JsonDocument.Parse(await ReadLineAsync(reader));
        using JsonDocument selection = JsonDocument.Parse(await ReadLineAsync(reader));
        Assert.Equal(
            "Microsoft.Maui.Controls.Button",
            toolbox.RootElement.GetProperty("toolboxItems")[0]
                .GetProperty("controlType")
                .GetString());
        Assert.Equal(
            "button-1",
            selection.RootElement.GetProperty("selection")
                .GetProperty("elementId")
                .GetString());

        await writer.WriteLineAsync(
            """{"type":"host.command","command":"setProperty","elementId":"button-1","propertyName":"Text","value":"Updated"}""");
        HostedDesignerCommand received = await command.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("setProperty", received.Name);
        Assert.Equal("button-1", received.ElementId);
        Assert.Equal("Text", received.PropertyName);
        Assert.Equal("Updated", received.Value);
    }

    [Fact]
    public async Task Bridge_requests_and_consumes_project_control_manifests()
    {
        string pipeName = $"MauiDesigner.Test.{Guid.NewGuid():N}";
        using var server = CreateServer(pipeName);
        using var bridge = new NamedPipeHostedDesignerBridge(pipeName);
        var received = new TaskCompletionSource<HostedProjectControls>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        bridge.ProjectControlsReceived += (_, controls) => received.TrySetResult(controls);
        bridge.ErrorReported += (_, message) => error.TrySetResult(message);

        bridge.Start();
        await server.WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(5));
        using var reader = new StreamReader(server);
        using var writer = new StreamWriter(server) { AutoFlush = true };
        await AssertHandshakeAsync(reader);

        await writer.WriteLineAsync("""
            {
              "type": "manifests.push",
              "target": "net10.0-windows10.0.19041.0/win-x64",
              "manifests": [],
              "assemblies": [
                {
                  "path": "C:\\packages\\Syncfusion.Maui.Buttons.dll",
                  "package": "Syncfusion.Maui.Buttons",
                  "isRoot": true
                }
              ],
              "startupMethods": [
                {
                  "package": "Syncfusion.Maui.Core",
                  "assembly": "Syncfusion.Maui.Core",
                  "type": "Syncfusion.Maui.Core.Hosting.AppHostBuilderExtensions",
                  "method": "ConfigureSyncfusionCore"
                }
              ],
              "diagnostics": []
            }
            """.ReplaceLineEndings(string.Empty));

        Task completed = await Task.WhenAny(received.Task, error.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));
        if (completed == error.Task)
        {
            Assert.Fail(await error.Task);
        }

        HostedProjectControls controls = await received.Task;
        Assert.Equal("net10.0-windows10.0.19041.0/win-x64", controls.Target);
        Assert.True(Assert.Single(controls.Assemblies).IsRoot);
        Assert.Equal("ConfigureSyncfusionCore", Assert.Single(controls.StartupMethods).Method);
    }

    private static async Task<string> ReadLineAsync(StreamReader reader) =>
        await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) ??
        throw new EndOfStreamException();

    private static async Task AssertHandshakeAsync(StreamReader reader)
    {
        Assert.Equal("designer.ready", MessageType(await ReadLineAsync(reader)));
        Assert.Equal("manifests.request", MessageType(await ReadLineAsync(reader)));
    }

    private static NamedPipeServerStream CreateServer(string pipeName) =>
        new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

    private static string? MessageType(string json)
    {
        using JsonDocument message = JsonDocument.Parse(json);
        return message.RootElement.GetProperty("type").GetString();
    }
}
