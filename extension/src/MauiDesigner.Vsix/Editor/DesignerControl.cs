using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using MauiDesigner.Core.Protocol;

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
        private readonly List<string> _pending = new List<string>();
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private readonly object _closeGate = new object();
        private NamedPipeServerStream? _pipe;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private Process? _process;
        private string? _pendingCloseRequestId;
        private ManualResetEventSlim? _pendingCloseResponse;
        private string? _pendingFinalXaml;
        private bool _isConnected;
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

            var root = new Grid();
            root.Children.Add(_nativeHost);
            root.Children.Add(_status);
            Content = root;
        }

        public event EventHandler<string>? MessageReceived;

        public async Task InitializeAsync(string executablePath)
        {
            if (!File.Exists(executablePath))
            {
                ShowStatus($"The native MAUI Designer was not found at {executablePath}.");
                return;
            }

            try
            {
                string pipeName = $"MauiDesigner.{Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}";
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
                _process = StartDesigner(executablePath, pipeName);
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
                _reader = new StreamReader(_pipe);
                _writer = new StreamWriter(_pipe) { AutoFlush = true };
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
            if (_disposed)
            {
                return;
            }

            lock (_pending)
            {
                if (!_isConnected)
                {
                    _pending.Add(json);
                    return;
                }
            }

            QueueWrite(json);
        }

        private static Process StartDesigner(string executablePath, string pipeName)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--designer-pipe \"{pipeName}\"",
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executablePath)
            });
            return process ?? throw new InvalidOperationException(
                "Windows did not create the MAUI Designer process.");
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
            catch (IOException) when (_disposed)
            {
            }

            if (!_disposed)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                ShowStatus("The native MAUI Designer disconnected.");
            }
        }

        private void FlushPending()
        {
            List<string> messages;
            lock (_pending)
            {
                messages = new List<string>(_pending);
                _pending.Clear();
            }

            foreach (string message in messages)
            {
                QueueWrite(message);
            }
        }

        private void QueueWrite(string json)
        {
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _writeGate.WaitAsync();
                try
                {
                    if (!_disposed && _writer is not null)
                    {
                        await _writer.WriteLineAsync(json);
                    }
                }
                catch (IOException) when (_disposed)
                {
                }
                finally
                {
                    _writeGate.Release();
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
            if (!_writeGate.Wait(timeout))
            {
                ClearCloseRequest(requestId);
                return null;
            }

            try
            {
                _writer.WriteLine(DesignerProtocol.HostClose(requestId));
                _writer.Flush();
            }
            catch (IOException)
            {
                ClearCloseRequest(requestId);
                return null;
            }
            finally
            {
                _writeGate.Release();
            }

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
            _nativeHost.Dispose();
            _writeGate.Dispose();
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
