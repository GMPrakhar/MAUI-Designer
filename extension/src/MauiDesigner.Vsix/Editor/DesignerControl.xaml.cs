using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using Microsoft.Web.WebView2.Core;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// Hosts the Angular designer in WebView2. The compiled application is served
    /// through a virtual host name because `file://` origins cannot use the
    /// browser storage and module loading the designer relies on.
    /// </summary>
    public partial class DesignerControl : UserControl, IDisposable
    {
        private const string VirtualHost = "maui-designer.invalid";

        private readonly TaskCompletionSource<bool> _ready =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposed;

        public DesignerControl()
        {
            InitializeComponent();
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
                await WebView.EnsureCoreWebView2Async(environment);

                var core = WebView.CoreWebView2;
                core.SetVirtualHostNameToFolderMapping(
                    VirtualHost,
                    webRootDirectory,
                    CoreWebView2HostResourceAccessKind.Allow);

                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.AreDevToolsEnabled = true;

                core.WebMessageReceived += OnWebMessageReceived;

                core.Navigate($"https://{VirtualHost}/index.html");

                StatusText.Visibility = Visibility.Collapsed;
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
                if (!await _ready.Task || _disposed || WebView.CoreWebView2 is null)
                {
                    return;
                }

                // The designer listens for `window` message events, so the payload
                // is delivered as a string and parsed there.
                WebView.CoreWebView2.PostWebMessageAsString(json);
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
            StatusText.Text = message;
            StatusText.Visibility = Visibility.Visible;
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

            if (WebView.CoreWebView2 is not null)
            {
                WebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            }

            WebView.Dispose();
        }
    }
}
