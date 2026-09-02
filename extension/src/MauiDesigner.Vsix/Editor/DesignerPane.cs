using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MauiDesigner.Core.Manifests;
using MauiDesigner.Core.Protocol;
using MauiDesigner.Vsix.Projects;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// The document window: hosts the WebView, and keeps the Visual Studio text
    /// buffer and the designer in sync in both directions.
    /// </summary>
    public sealed class DesignerPane : WindowPane
    {
        private readonly IVsTextLines _textLines;
        private readonly string _documentMoniker;
        private readonly IVsHierarchy _hierarchy;
        private readonly DesignerControl _control;
        private readonly DesignerSession _session;
        private ITextBuffer? _textBuffer;
        private CancellationTokenSource? _bufferReloadCancellation;

        /// <summary>
        /// Taken from the package rather than <see cref="ThreadHelper"/>, whose
        /// tasks deliberately do not block the IDE from exiting. This pane writes
        /// the user's designer edits back into the text buffer, so losing that
        /// work to a shutdown race would mean losing their changes.
        /// </summary>
        private readonly JoinableTaskFactory _joinableTaskFactory;

        private bool _applyingDesignerEdit;
        private bool _disposed;

        public DesignerPane(
            AsyncPackage package,
            IVsTextLines textLines,
            string documentMoniker,
            IVsHierarchy hierarchy)
            : base(package)
        {
            if (package is null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _joinableTaskFactory = package.JoinableTaskFactory;
            _textLines = textLines ?? throw new ArgumentNullException(nameof(textLines));
            _documentMoniker = documentMoniker;
            _hierarchy = hierarchy;

            _control = new DesignerControl(_joinableTaskFactory);
            _session = new DesignerSession(_control.PostMessage);

            _control.MessageReceived += (_, json) => _session.HandleMessage(json);
            _session.DocumentChanged += OnDesignerEdited;
            _session.SaveRequested += OnSaveRequested;
            _session.ManifestsRequested += OnManifestsRequested;
            _session.ErrorReported += (_, message) => WriteToOutput(message);
        }

        /// <inheritdoc />
        public override object Content => _control;

        /// <inheritdoc />
        protected override void Initialize()
        {
            base.Initialize();

            _joinableTaskFactory.RunAsync(async () =>
            {
                await _control.InitializeAsync(WebAssetLocator.WebRootDirectory);
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                if (_disposed)
                {
                    return;
                }

                SubscribeToBufferChanges();
                _session.OpenDocument(ReadBuffer(), _documentMoniker);
            }).FileAndForget("vs/mauidesigner/initialize");

            ThreadHelper.ThrowIfNotOnUIThread();
            _session.OpenDocument(ReadBuffer(), _documentMoniker);
        }

        private void SubscribeToBufferChanges()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_textBuffer is not null)
            {
                return;
            }

            var componentModel = GetService(typeof(SComponentModel)) as IComponentModel
                ?? throw new InvalidOperationException("Visual Studio's component model is unavailable.");
            var adapters = componentModel.GetService<IVsEditorAdaptersFactoryService>()
                ?? throw new InvalidOperationException("Visual Studio's editor adapter service is unavailable.");

            _textBuffer = adapters.GetDataBuffer(_textLines)
                ?? throw new InvalidOperationException("The XAML document has no shared text buffer.");
            _textBuffer.Changed += OnTextBufferChanged;
        }

        private void OnDesignerEdited(object sender, DocumentChangedEventArgs args)
        {
            if (Volatile.Read(ref _bufferReloadCancellation) is not null)
            {
                return;
            }

            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (_bufferReloadCancellation is not null)
                {
                    return;
                }

                WriteBuffer(args.Xaml);
            }).FileAndForget("vs/mauidesigner/documentchanged");
        }

        private void OnSaveRequested(object sender, DocumentSaveRequestedEventArgs args)
        {
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                WriteBuffer(args.Xaml);

                if (GetService(typeof(SVsRunningDocumentTable)) is IVsRunningDocumentTable4 table &&
                    GetService(typeof(SVsSolution)) is IVsSolution solution)
                {
                    var cookie = table.GetDocumentCookie(_documentMoniker);
                    solution.SaveSolutionElement(
                        (uint)__VSSLNSAVEOPTIONS.SLNSAVEOPT_SaveIfDirty,
                        _hierarchy,
                        cookie);
                }

                _session.NotifySaved();
            }).FileAndForget("vs/mauidesigner/save");
        }

        private void OnManifestsRequested(object sender, EventArgs args)
        {
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                var projectFile = ProjectManifestProvider.FindProjectFile(_hierarchy, _documentMoniker);

                await TaskScheduler.Default;
                IReadOnlyList<CustomControlManifest> manifests;
                try
                {
                    manifests = ProjectManifestProvider.ForProject(projectFile);
                }
                catch (Exception error)
                {
                    WriteToOutput($"Could not read the project's NuGet controls: {error.Message}");
                    return;
                }

                await _joinableTaskFactory.SwitchToMainThreadAsync();
                _session.PushManifests(manifests);
            }).FileAndForget("vs/mauidesigner/manifests");
        }

        private string ReadBuffer()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ErrorHandler.ThrowOnFailure(_textLines.GetLineCount(out var lineCount));
            ErrorHandler.ThrowOnFailure(_textLines.GetLengthOfLine(lineCount - 1, out var lastLineLength));
            ErrorHandler.ThrowOnFailure(
                _textLines.GetLineText(0, 0, lineCount - 1, lastLineLength, out var text));

            return text ?? string.Empty;
        }

        private void WriteBuffer(string xaml)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var textBuffer = _textBuffer
                ?? throw new InvalidOperationException("The XAML document has no shared text buffer.");
            var snapshot = textBuffer.CurrentSnapshot;

            if (_applyingDesignerEdit || snapshot.GetText() == xaml)
            {
                return;
            }

            _applyingDesignerEdit = true;
            try
            {
                textBuffer.Replace(new Span(0, snapshot.Length), xaml);
            }
            finally
            {
                _applyingDesignerEdit = false;
            }
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (_applyingDesignerEdit || _disposed)
            {
                return;
            }

            var xaml = args.After.GetText();
            var cancellation = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref _bufferReloadCancellation, cancellation);
            previous?.Cancel();

            _joinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await Task.Delay(250, cancellation.Token);
                    await _joinableTaskFactory.SwitchToMainThreadAsync(cancellation.Token);

                    if (!_applyingDesignerEdit && !_disposed)
                    {
                        _session.OpenDocument(xaml, _documentMoniker);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                finally
                {
                    Interlocked.CompareExchange(ref _bufferReloadCancellation, null, cancellation);
                    cancellation.Dispose();
                }
            }).FileAndForget("vs/mauidesigner/bufferchanged");
        }

        private void WriteToOutput(string message)
        {
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (GetService(typeof(SVsGeneralOutputWindowPane)) is IVsOutputWindowPane pane)
                {
                    pane.OutputStringThreadSafe($"MAUI Designer: {message}{Environment.NewLine}");
                }
            }).FileAndForget("vs/mauidesigner/output");
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (disposing)
            {
                _disposed = true;

                var cancellation = Interlocked.Exchange(ref _bufferReloadCancellation, null);
                cancellation?.Cancel();
                cancellation?.Dispose();

                if (_textBuffer is not null)
                {
                    _textBuffer.Changed -= OnTextBufferChanged;
                    _textBuffer = null;
                }

                _control.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
