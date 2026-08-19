using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// Hosts the Angular designer in WebView2. The compiled application is served
    /// through a virtual host name because `file://` origins cannot use the
    /// browser storage and module loading the designer relies on.
    /// </summary>
    /// <remarks>
    /// The UI is built in code rather than in XAML on purpose: it is a single WebView with a
    /// status message, and avoiding markup compilation keeps the whole assembly buildable (and
    /// therefore compile-checkable in CI) on any operating system.
    /// </remarks>
    public sealed class DesignerControl : UserControl, IDisposable
    {
        private const string VirtualHost = "maui-designer.invalid";

        private readonly WebView2 _webView;
        private readonly TextBlock _status;

        /// <summary>
        /// Messages posted before WebView2 finished booting. The session starts
        /// talking to the designer as soon as the document opens, which is
        /// routinely before the browser is ready. Only touched on the UI thread.
        /// </summary>
        private readonly List<string> _pending = new List<string>();

        private bool _isReady;
        private bool _disposed;

        /// <summary>
        /// Supplied by the package rather than taken from <see cref="ThreadHelper"/>
        /// so queued messages still block the IDE from exiting mid-flight.
        /// </summary>
        private readonly JoinableTaskFactory _joinableTaskFactory;

        public DesignerControl(JoinableTaskFactory joinableTaskFactory)
        {
            _joinableTaskFactory = joinableTaskFactory ?? throw new ArgumentNullException(nameof(joinableTaskFactory));

            _webView = new WebView2();

            _status = new TextBlock
            {
                Text = "Starting the MAUI designer...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var root = new Grid();
            root.Children.Add(_webView);
            root.Children.Add(_status);
            Content = root;
        }

        /// <summary>Raised for every JSON message the designer posts to the host.</summary>
        public event EventHandler<string>? MessageReceived;

        /// <summary>Boots WebView2 and navigates to the bundled designer.</summary>
        public async Task InitializeAsync(string webRootDirectory)
        {
            try
            {
                if (!Directory.Exists(webRootDirectory))
                {
                    ShowStatus($"The designer web assets were not found at {webRootDirectory}.");
                    return;
                }

                // Keep the user profile out of the VS install directory, which is read-only.
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MauiDesigner",
                    "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(environment);

                // WebView2 resumes on the UI thread here in practice, but say so
                // explicitly rather than relying on it: everything below touches
                // WPF state.
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                var core = _webView.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(
                    VirtualHost,
                    webRootDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);

                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDevToolsEnabled = true;

                core.WebMessageReceived += OnWebMessageReceived;

                core.Navigate($"https://{VirtualHost}/index.html");

                _status.Visibility = Visibility.Collapsed;

                _isReady = true;
                FlushPending();
            }
            catch (Exception error)
            {
                ShowStatus($"The designer could not start: {error.Message}");
                _pending.Clear();
            }
        }

        /// <summary>Posts a JSON payload to the designer. Safe to call before it is ready.</summary>
        public void PostMessage(string json)
        {
            if (_disposed)
            {
                return;
            }

            // Callers include the manifest scan, which runs on a background
            // thread. Marshal through the JoinableTaskFactory rather than the
            // dispatcher so the work joins Visual Studio's own task tracking
            // instead of racing it during shutdown.
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (_disposed)
                {
                    return;
                }

                if (!_isReady)
                {
                    _pending.Add(json);
                    return;
                }

                Send(json);
            }).FileAndForget("vs/mauidesigner/postmessage");
        }

        private void FlushPending()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (var message in _pending)
            {
                Send(message);
            }

            _pending.Clear();
        }

        private void Send(string json)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_disposed || _webView.CoreWebView2 is null)
            {
                return;
            }

            // The designer listens for `window` message events, so the payload
            // is delivered as a string and parsed there.
            _webView.CoreWebView2.PostWebMessageAsString(json);
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string json;
            try
            {
                json = args.TryGetWebMessageAsString();
            }
            catch (ArgumentException)
            {
                json = args.WebMessageAsJson;
            }

            MessageReceived?.Invoke(this, json);
        }

        private void ShowStatus(string message)
        {
            _status.Text = message;
            _status.Visibility = Visibility.Visible;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending.Clear();

            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }

            _webView.Dispose();
        }
    }
}
