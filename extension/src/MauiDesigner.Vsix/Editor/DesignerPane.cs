using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using MauiDesigner.Core.Manifests;
using MauiDesigner.Core.Protocol;
using MauiDesigner.Vsix.Projects;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
        private bool _applyingDesignerEdit;

        public DesignerPane(
            IServiceProvider serviceProvider,
            IVsTextLines textLines,
            string documentMoniker,
            IVsHierarchy hierarchy)
            : base(serviceProvider)
        {
            _textLines = textLines ?? throw new ArgumentNullException(nameof(textLines));
            _documentMoniker = documentMoniker;
            _hierarchy = hierarchy;

            _control = new DesignerControl();
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

            _ = _control.InitializeAsync(WebAssetLocator.WebRootDirectory);

            ThreadHelper.ThrowIfNotOnUIThread();
            _session.OpenDocument(ReadBuffer(), _documentMoniker);
        }

        private void OnDesignerEdited(object sender, DocumentChangedEventArgs args)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                WriteBuffer(args.Xaml);
            });
        }

        private void OnSaveRequested(object sender, DocumentSaveRequestedEventArgs args)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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
            });
        }

        private void OnManifestsRequested(object sender, EventArgs args)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
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

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _session.PushManifests(manifests);
            });
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

            if (_applyingDesignerEdit || ReadBuffer() == xaml)
            {
                return;
            }

            _applyingDesignerEdit = true;
            try
            {
                ErrorHandler.ThrowOnFailure(_textLines.GetLineCount(out var lineCount));
                ErrorHandler.ThrowOnFailure(_textLines.GetLengthOfLine(lineCount - 1, out var lastLineLength));

                var bytes = System.Text.Encoding.Unicode.GetBytes(xaml);
                var buffer = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(bytes.Length + 2);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(bytes, 0, buffer, bytes.Length);
                    System.Runtime.InteropServices.Marshal.WriteInt16(buffer, bytes.Length, 0);

                    ErrorHandler.ThrowOnFailure(_textLines.ReplaceLines(
                        0, 0, lineCount - 1, lastLineLength, buffer, xaml.Length, null));
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeCoTaskMem(buffer);
                }
            }
            finally
            {
                _applyingDesignerEdit = false;
            }
        }

        private void WriteToOutput(string message)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (GetService(typeof(SVsGeneralOutputWindowPane)) is IVsOutputWindowPane pane)
                {
                    pane.OutputStringThreadSafe($"MAUI Designer: {message}{Environment.NewLine}");
                }
            });
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _control.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
