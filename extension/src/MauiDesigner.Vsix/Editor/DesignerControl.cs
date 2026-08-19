using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;

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

        private readonly TaskCompletionSource<bool> _ready =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposed;

        public DesignerControl()
        {
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
                _ready.TrySetResult(true);
            }
            catch (Exception error)
            {
                ShowStatus($"The designer could not start: {error.Message}");
                _ready.TrySetResult(false);
            }
        }

        /// <summary>Posts a JSON payload to the designer. Safe to call before it is ready.</summary>
        public void PostMessage(string json)
        {
            if (_disposed)
            {
                return;
            }

            _ = Dispatcher.InvokeAsync(async () =>
            {
                if (!await _ready.Task || _disposed || _webView.CoreWebView2 is null)
                {
                    return;
                }

                // The designer listens for `window` message events, so the payload
                // is delivered as a string and parsed there.
                _webView.CoreWebView2.PostWebMessageAsString(json);
            });
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
            _ready.TrySetResult(false);

            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }

            _webView.Dispose();
        }
    }
}
