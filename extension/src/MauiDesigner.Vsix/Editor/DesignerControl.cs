using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using MauiDesigner.Core.Manifests;
using MauiDesigner.Core.Protocol;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// Hosts a self-contained MAUI Designer process inside the editor pane and
    /// transports document messages over a private named pipe.
    /// </summary>
    public sealed class DesignerControl : UserControl, IDisposable
    {
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly NativeDesignerHost _nativeHost;
        private readonly TextBlock _status;
        private readonly Dictionary<string, Button> _commandButtons =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly LinkedList<string> _pending = new LinkedList<string>();
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private readonly object _closeGate = new object();
        private NamedPipeServerStream? _pipe;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private Process? _process;
        private string? _startupManifestPath;
        private string? _pendingCloseRequestId;
        private ManualResetEventSlim? _pendingCloseResponse;
        private string? _pendingFinalXaml;
        private bool _isConnected;
        private bool _writePumpRunning;
        private bool _disposed;

        public DesignerControl(JoinableTaskFactory joinableTaskFactory)
        {
            _joinableTaskFactory = joinableTaskFactory ??
                throw new ArgumentNullException(nameof(joinableTaskFactory));
            _nativeHost = new NativeDesignerHost();
            _status = new TextBlock
            {
                Text = "Starting the native MAUI Designer...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _nativeHost.ShortcutRequested += OnShortcutRequested;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.Children.Add(CreateToolbar());
            Grid.SetRow(_nativeHost, 1);
            Grid.SetRow(_status, 1);
            root.Children.Add(_nativeHost);
            root.Children.Add(_status);
            Content = root;
        }

        public event EventHandler<string>? MessageReceived;

        private void OnShortcutRequested(object? sender, string command) =>
            PostMessage(DesignerProtocol.HostCommand(command));

        private ToolBarTray CreateToolbar()
        {
            var toolbar = new ToolBar
            {
                Band = 0,
                BandIndex = 0
            };
            toolbar.SetResourceReference(
                Control.BackgroundProperty,
                EnvironmentColors.CommandBarGradientBrushKey);
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Undo, "Undo (Ctrl+Z)", "undo"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Redo, "Redo (Ctrl+Y)", "redo"));
            toolbar.Items.Add(new Separator());
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Cut, "Cut (Ctrl+X)", "cut"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Copy, "Copy (Ctrl+C)", "copy"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Paste, "Paste (Ctrl+V)", "paste"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Copy, "Duplicate (Ctrl+D)", "duplicate"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Delete, "Delete", "delete"));
            toolbar.Items.Add(new Separator());
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.ZoomOut, "Zoom out", "zoomOut"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.ZoomIn, "Zoom in", "zoomIn"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.ZoomToFit, "Fit canvas", "zoomFit"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Zoom, "Actual size", "zoomReset"));
            toolbar.Items.Add(new Separator());
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.SnapToGrid, "Toggle snapping", "toggleSnap"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Grid, "Toggle grid", "toggleGrid"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Ruler, "Toggle rulers", "toggleRulers"));
            toolbar.Items.Add(new Separator());
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.OpenFile, "Load custom controls", "loadControls"));
            toolbar.Items.Add(CreateToolbarButton(KnownMonikers.Run, "Run preview", "runPreview"));

            var tray = new ToolBarTray
            {
                IsLocked = true
            };
            tray.SetResourceReference(
                ToolBarTray.BackgroundProperty,
                EnvironmentColors.CommandBarGradientBrushKey);
            tray.ToolBars.Add(toolbar);
            return tray;
        }

        private Button CreateToolbarButton(
            ImageMoniker moniker,
            string tooltip,
            string command)
        {
            var image = new CrispImage
            {
                Moniker = moniker,
                Width = 16,
                Height = 16,
                SnapsToDevicePixels = true
            };
            var imageHolder = new ContentControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Content = image,
                Height = 20,
                Width = 20
            };
            imageHolder.SetResourceReference(
                ImageThemingUtilities.ImageBackgroundColorProperty,
                EnvironmentColors.CommandBarGradientBeginColorKey);

            var button = new Button
            {
                Width = 28,
                Height = 26,
                Padding = new Thickness(4),
                ToolTip = tooltip,
                Content = imageHolder
            };
            System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
            button.Click += (_, _) => PostMessage(DesignerProtocol.HostCommand(command));
            _commandButtons[command] = button;
            return button;
        }

        public void UpdateCommandState(DesignerSelectionSnapshot selection)
        {
            SetCommandEnabled("undo", selection.CanUndo);
            SetCommandEnabled("redo", selection.CanRedo);
            SetCommandEnabled("copy", selection.CanCopy);
            SetCommandEnabled("cut", selection.CanCut);
            SetCommandEnabled("paste", selection.CanPaste);
            SetCommandEnabled("duplicate", selection.CanDuplicate);
            SetCommandEnabled("delete", selection.CanDelete);
        }

        private void SetCommandEnabled(string command, bool enabled)
        {
            if (_commandButtons.TryGetValue(command, out Button? button))
            {
                button.IsEnabled = enabled;
            }
        }

        public async Task InitializeAsync(
            string executablePath,
            ProjectControlManifest projectControls)
        {
            if (!File.Exists(executablePath))
            {
                ShowStatus($"The native MAUI Designer was not found at {executablePath}.");
                return;
            }

            try
            {
                string pipeName = $"MauiDesigner.{Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}";
                _startupManifestPath = WriteStartupManifest(projectControls);
                _pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Task connection = Task.Factory.FromAsync(
                    _pipe.BeginWaitForConnection,
                    _pipe.EndWaitForConnection,
                    null);
                IntPtr hostHandle = await _nativeHost.WaitForHandleAsync();
                _process = StartDesigner(executablePath, pipeName, _startupManifestPath);
                IntPtr designerHandle = await WaitForMainWindowAsync(_process, TimeSpan.FromSeconds(20));
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                if (_disposed)
                {
                    return;
                }

                _nativeHost.Attach(designerHandle);
                await WaitForPipeConnectionAsync(
                    _pipe,
                    connection,
                    TimeSpan.FromSeconds(20));
                CreatePipeStreams();
                _isConnected = true;
                _status.Visibility = Visibility.Collapsed;
                FlushPending();

                _joinableTaskFactory.RunAsync(ReadMessagesAsync)
                    .FileAndForget("vs/mauidesigner/native-read");
            }
            catch (Exception error) when (
                error is IOException or
                Win32Exception or
                InvalidOperationException or
                TimeoutException ||
                error is ObjectDisposedException && _disposed)
            {
                ShowStatus($"The native MAUI Designer could not start: {error.Message}");
                _pipe?.Dispose();
                _pipe = null;
                DisposeProcess();
            }
        }

        public void PostMessage(string json)
        {
            bool startWriter = false;
            if (_disposed)
            {
                return;
            }

            lock (_pending)
            {
                _pending.AddLast(json);
                if (_isConnected && !_writePumpRunning)
                {
                    _writePumpRunning = true;
                    startWriter = true;
                }
            }

            if (startWriter)
            {
                StartWritePump();
            }
        }

        private static Process StartDesigner(
            string executablePath,
            string pipeName,
            string startupManifestPath)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments =
                    $"--designer-pipe \"{pipeName}\" --designer-startup-manifest \"{startupManifestPath}\"",
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executablePath)
            });
            return process ?? throw new InvalidOperationException(
                "Windows did not create the MAUI Designer process.");
        }

        private static string WriteStartupManifest(ProjectControlManifest projectControls)
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MauiDesigner",
                "Startup");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, ManifestJson.Serialize(projectControls), Encoding.UTF8);
            return path;
        }

        private static async Task<IntPtr> WaitForMainWindowAsync(
            Process process,
            TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"The MAUI Designer exited with code {process.ExitCode}.");
                }

                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("The MAUI Designer did not create a window.");
        }

        // Both tasks are asynchronous I/O created by InitializeAsync. This helper
        // intentionally awaits caller-owned tasks so it can bound pipe startup.
#pragma warning disable VSTHRD003
        private static async Task WaitForPipeConnectionAsync(
            NamedPipeServerStream pipe,
            Task connection,
            TimeSpan timeout)
        {
            if (await Task.WhenAny(connection, Task.Delay(timeout)) == connection)
            {
                await connection;
                return;
            }

            pipe.Dispose();
            try
            {
                await connection;
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            throw new TimeoutException("The native MAUI Designer did not connect to its document channel.");
        }
#pragma warning restore VSTHRD003

        private async Task ReadMessagesAsync()
        {
            while (!_disposed)
            {
                try
                {
                    while (!_disposed && _reader is not null)
                    {
                        string? json = await _reader.ReadLineAsync().ConfigureAwait(false);
                        if (json is null)
                        {
                            break;
                        }

                        DesignerMessage? message = DesignerProtocol.Parse(json);
                        if (message?.Type == MessageTypes.DesignerFocusChanged)
                        {
                            _nativeHost.TextInputFocused = message.TextInputFocused == true;
                            continue;
                        }

                        if (message?.Type == MessageTypes.DesignerClosed)
                        {
                            lock (_closeGate)
                            {
                                if (_pendingCloseRequestId is not null &&
                                    DesignerProtocol.IsCloseResponseFor(
                                        message,
                                        _pendingCloseRequestId))
                                {
                                    _pendingFinalXaml = message.Xaml;
                                    _pendingCloseResponse?.Set();
                                }
                            }

                            continue;
                        }

                        MessageReceived?.Invoke(this, json);
                    }
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException) when (_disposed)
                {
                    return;
                }

                if (_disposed || !await ReconnectAsync())
                {
                    return;
                }
            }
        }

        private async Task<bool> ReconnectAsync()
        {
            NamedPipeServerStream? pipe = _pipe;
            if (_disposed || pipe is null)
            {
                return false;
            }

            lock (_pending)
            {
                _isConnected = false;
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync();
            ShowStatus("The native MAUI Designer disconnected. Reconnecting...");

            await _writeGate.WaitAsync();
            try
            {
                _reader?.Dispose();
                _writer?.Dispose();
                _reader = null;
                _writer = null;
                if (pipe.IsConnected)
                {
                    pipe.Disconnect();
                }

                Task connection = Task.Factory.FromAsync(
                    pipe.BeginWaitForConnection,
                    pipe.EndWaitForConnection,
                    null);
                await connection.ConfigureAwait(false);
                if (_disposed)
                {
                    return false;
                }

                CreatePipeStreams();
                lock (_pending)
                {
                    _isConnected = true;
                }
            }
            catch (Exception error) when (
                error is IOException or InvalidOperationException or ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                _writeGate.Release();
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync();
            _status.Visibility = Visibility.Collapsed;
            FlushPending();
            return true;
        }

        private void CreatePipeStreams()
        {
            if (_pipe is null)
            {
                throw new InvalidOperationException("The designer pipe is unavailable.");
            }

            _reader = new StreamReader(
                _pipe,
                System.Text.Encoding.UTF8,
                true,
                1024,
                leaveOpen: true);
            _writer = new StreamWriter(
                _pipe,
                System.Text.Encoding.UTF8,
                1024,
                leaveOpen: true);
        }

        private void FlushPending()
        {
            bool startWriter = false;
            lock (_pending)
            {
                if (_pending.Count > 0 && !_writePumpRunning)
                {
                    _writePumpRunning = true;
                    startWriter = true;
                }
            }

            if (startWriter)
            {
                StartWritePump();
            }
        }

        private void StartWritePump()
        {
            _joinableTaskFactory.RunAsync(async () =>
            {
                while (true)
                {
                    string json;
                    lock (_pending)
                    {
                        if (_disposed || !_isConnected || _pending.First is null)
                        {
                            _writePumpRunning = false;
                            return;
                        }

                        json = _pending.First.Value;
                        _pending.RemoveFirst();
                    }

                    await _writeGate.WaitAsync();
                    try
                    {
                        if (_disposed || _writer is null)
                        {
                            lock (_pending)
                            {
                                if (!_disposed)
                                {
                                    _pending.AddFirst(json);
                                }

                                _writePumpRunning = false;
                            }

                            return;
                        }

                        await _writer.WriteLineAsync(json);
                        await _writer.FlushAsync();
                    }
                    catch (Exception error) when (
                        error is IOException or InvalidOperationException or ObjectDisposedException)
                    {
                        lock (_pending)
                        {
                            if (!_disposed)
                            {
                                _pending.AddFirst(json);
                                _isConnected = false;
                            }

                            _writePumpRunning = false;
                        }

                        if (!_disposed)
                        {
                            await _joinableTaskFactory.SwitchToMainThreadAsync();
                            ShowStatus("The native MAUI Designer connection was interrupted. Reconnecting...");
                        }

                        return;
                    }
                    finally
                    {
                        _writeGate.Release();
                    }
                }
            }).FileAndForget("vs/mauidesigner/native-write");
        }

        private void ShowStatus(string message)
        {
            _status.Text = message;
            _status.Visibility = Visibility.Visible;
        }

        public string? RequestFinalXaml(TimeSpan timeout)
        {
            if (!_isConnected || _writer is null || timeout <= TimeSpan.Zero)
            {
                return null;
            }

            string requestId = Guid.NewGuid().ToString("N");
            using var response = new ManualResetEventSlim(false);
            lock (_closeGate)
            {
                if (_pendingCloseRequestId is not null)
                {
                    return null;
                }

                _pendingCloseRequestId = requestId;
                _pendingCloseResponse = response;
                _pendingFinalXaml = null;
            }

            var stopwatch = Stopwatch.StartNew();
            PostMessage(DesignerProtocol.HostClose(requestId));

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                response.Wait(remaining);
            }

            lock (_closeGate)
            {
                string? finalXaml =
                    _pendingCloseRequestId == requestId && response.IsSet
                        ? _pendingFinalXaml
                        : null;
                ClearCloseRequestUnsafe(requestId);
                return finalXaml;
            }
        }

        private void ClearCloseRequest(string requestId)
        {
            lock (_closeGate)
            {
                ClearCloseRequestUnsafe(requestId);
            }
        }

        private void ClearCloseRequestUnsafe(string requestId)
        {
            if (_pendingCloseRequestId != requestId)
            {
                return;
            }

            _pendingCloseRequestId = null;
            _pendingCloseResponse = null;
            _pendingFinalXaml = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isConnected = false;
            lock (_pending)
            {
                _pending.Clear();
            }

            _reader?.Dispose();
            _writer?.Dispose();
            _pipe?.Dispose();
            DisposeProcess();
            if (_startupManifestPath is not null)
            {
                try
                {
                    File.Delete(_startupManifestPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                _startupManifestPath = null;
            }

            _nativeHost.ShortcutRequested -= OnShortcutRequested;
            _nativeHost.Dispose();
        }

        private void DisposeProcess()
        {
            Process? process = _process;
            _process = null;
            if (process is null)
            {
                return;
            }

            if (!process.HasExited)
            {
                process.Kill();
            }

            process.Dispose();
        }
    }
}
