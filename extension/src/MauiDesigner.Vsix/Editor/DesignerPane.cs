using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using MauiDesigner.Core.Manifests;
using MauiDesigner.Core.Protocol;
using MauiDesigner.Vsix.Projects;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;

using IOleDataObject = Microsoft.VisualStudio.OLE.Interop.IDataObject;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// The document window: hosts the native designer, and keeps the Visual Studio text
    /// buffer and the designer in sync in both directions.
    /// </summary>
    public sealed class DesignerPane :
        WindowPane,
        IVsWindowFrameNotify3,
        IVsToolboxUser
    {
        private const string ToolboxTabName = "MAUI Designer";
        private readonly IVsTextLines _textLines;
        private readonly string _documentMoniker;
        private readonly IVsHierarchy _hierarchy;
        private readonly DesignerControl _control;
        private readonly DesignerSession _session;
        private readonly List<OleDataObject> _toolboxDataObjects = new List<OleDataObject>();
        private ITextBuffer? _textBuffer;
        private IVsWindowFrame? _registeredFrame;
        private CancellationTokenSource? _bufferReloadCancellation;
        private SelectionContainer? _selectionContainer;
        private DesignerSelectionSnapshot? _lastSelection;
        private IReadOnlyList<DesignerToolboxItem>? _lastToolboxItems;
        private bool _toolWindowsShown;

        /// <summary>
        /// Taken from the package rather than <see cref="ThreadHelper"/>, whose
        /// tasks deliberately do not block the IDE from exiting. This pane writes
        /// the user's designer edits back into the text buffer, so losing that
        /// work to a shutdown race would mean losing their changes.
        /// </summary>
        private readonly JoinableTaskFactory _joinableTaskFactory;

        private bool _applyingDesignerEdit;
        private int _closeAttemptActive;
        private int _designerEditGeneration;
        private int _disposed;

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
            _session.ToolboxChanged += OnToolboxChanged;
            _session.SelectionChanged += OnSelectionChanged;
            _session.ErrorReported += (_, message) => WriteToOutput(message);
        }

        /// <inheritdoc />
        public override object Content => _control;

        /// <inheritdoc />
        protected override void Initialize()
        {
            base.Initialize();
            ThreadHelper.ThrowIfNotOnUIThread();
            RegisterFrameNotifications();

            _joinableTaskFactory.RunAsync(async () =>
            {
                await _control.InitializeAsync(NativeDesignerLocator.ExecutablePath);
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                if (Volatile.Read(ref _disposed) != 0)
                {
                    return;
                }

                RegisterFrameNotifications();
                SubscribeToBufferChanges();
                _session.OpenDocument(ReadBuffer(), _documentMoniker);
            }).FileAndForget("vs/mauidesigner/initialize");
        }

        private void OnToolboxChanged(
            object sender,
            IReadOnlyList<DesignerToolboxItem> items)
        {
            _lastToolboxItems = items;
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                if (Volatile.Read(ref _disposed) == 0)
                {
                    PopulateToolbox(items);
                }
            }).FileAndForget("vs/mauidesigner/toolbox");
        }

        private void PopulateToolbox(IReadOnlyList<DesignerToolboxItem> items)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (GetService(typeof(SVsToolbox)) is not IVsToolbox toolbox)
            {
                return;
            }

            toolbox.RemoveTab(ToolboxTabName);
            ErrorHandler.ThrowOnFailure(toolbox.AddTab(ToolboxTabName));
            _toolboxDataObjects.Clear();
            foreach (DesignerToolboxItem item in items)
            {
                var data = new OleDataObject();
                data.SetData(
                    DesignerToolboxPayload.DataFormat,
                    item.ControlType);
                _toolboxDataObjects.Add(data);
                var itemInfo = new[]
                {
                    new TBXITEMINFO
                    {
                        bstrText = item.DisplayName,
                        hBmp = IntPtr.Zero,
                        dwFlags = (uint)__TBXITEMINFOFLAGS.TBXIF_DONTPERSIST
                    }
                };
                ErrorHandler.ThrowOnFailure(
                    toolbox.AddItem(data, itemInfo, ToolboxTabName));
            }

            if (!_toolWindowsShown)
            {
                ShowToolWindow(new Guid(ToolWindowGuids80.Toolbox));
                ShowToolWindow(new Guid(ToolWindowGuids.PropertyBrowser));
                _toolWindowsShown = true;
            }
        }

        private void ShowToolWindow(Guid persistenceSlot)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (GetService(typeof(SVsUIShell)) is not IVsUIShell shell)
            {
                return;
            }

            ErrorHandler.ThrowOnFailure(shell.FindToolWindow(
                (uint)__VSFINDTOOLWIN.FTW_fForceCreate,
                ref persistenceSlot,
                out IVsWindowFrame frame));
            ErrorHandler.ThrowOnFailure(frame.ShowNoActivate());
        }

        private void OnSelectionChanged(object sender, DesignerSelectionSnapshot selection)
        {
            _lastSelection = selection;
            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();
                if (Volatile.Read(ref _disposed) == 0)
                {
                    _control.UpdateCommandState(selection);
                    PublishSelection(selection);
                }
            }).FileAndForget("vs/mauidesigner/selection");
        }

        private void PublishSelection(DesignerSelectionSnapshot selection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (GetService(typeof(STrackSelection)) is not ITrackSelection trackSelection)
            {
                return;
            }

            var selected = new ArrayList();
            if (selection.SelectionCount > 0)
            {
                selected.Add(new DesignerSelectionProxy(
                    selection,
                    (name, value) =>
                    {
                        if (selection.ElementId is not null)
                        {
                            _control.PostMessage(DesignerProtocol.HostSetProperty(
                                selection.ElementId,
                                name,
                                value));
                        }
                    }));
            }

            _selectionContainer = new SelectionContainer(true, false)
            {
                SelectableObjects = selected,
                SelectedObjects = selected
            };
            ErrorHandler.ThrowOnFailure(trackSelection.OnSelectChange(_selectionContainer));
        }

        int IVsToolboxUser.IsSupported(IOleDataObject dataObject) =>
            TryGetToolboxControlType(dataObject, out _)
                ? VSConstants.S_OK
                : VSConstants.S_FALSE;

        int IVsToolboxUser.ItemPicked(IOleDataObject dataObject)
        {
            if (!TryGetToolboxControlType(dataObject, out string? controlType) ||
                controlType is null)
            {
                return VSConstants.S_FALSE;
            }

            _control.PostMessage(DesignerProtocol.HostInsertControl(controlType));
            return VSConstants.S_OK;
        }

        private static bool TryGetToolboxControlType(
            IOleDataObject dataObject,
            out string? controlType)
        {
            var managed = new OleDataObject(dataObject);
            if (!managed.GetDataPresent(DesignerToolboxPayload.DataFormat))
            {
                controlType = null;
                return false;
            }

            controlType = managed.GetData(DesignerToolboxPayload.DataFormat) as string;
            return !string.IsNullOrWhiteSpace(controlType);
        }

        private void RegisterFrameNotifications()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_registeredFrame is not null)
            {
                return;
            }

            if (GetService(typeof(SVsWindowFrame)) is not IVsWindowFrame frame)
            {
                return;
            }

            ErrorHandler.ThrowOnFailure(
                frame.SetProperty((int)__VSFPROPID.VSFPROPID_ViewHelper, this));
            _registeredFrame = frame;
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
            var generation = Volatile.Read(ref _designerEditGeneration);
            if (!DesignerSession.ShouldAcceptDesignerEdit(
                    Volatile.Read(ref _closeAttemptActive) != 0 ||
                        Volatile.Read(ref _disposed) != 0,
                    Volatile.Read(ref _bufferReloadCancellation) is not null,
                    generation,
                    Volatile.Read(ref _designerEditGeneration)))
            {
                return;
            }

            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (!DesignerSession.ShouldAcceptDesignerEdit(
                        Volatile.Read(ref _closeAttemptActive) != 0 ||
                            Volatile.Read(ref _disposed) != 0,
                        _bufferReloadCancellation is not null,
                        generation,
                        Volatile.Read(ref _designerEditGeneration)))
                {
                    return;
                }

                if (!_session.CanAcceptDesignerEdit(args))
                {
                    return;
                }

                WriteBuffer(args.Xaml);
                _session.AcceptDesignerEdit(args);
            }).FileAndForget("vs/mauidesigner/documentchanged");
        }

        private void OnSaveRequested(object sender, DocumentSaveRequestedEventArgs args)
        {
            var generation = Volatile.Read(ref _designerEditGeneration);
            if (Volatile.Read(ref _closeAttemptActive) != 0 ||
                Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            _joinableTaskFactory.RunAsync(async () =>
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync();

                if (Volatile.Read(ref _closeAttemptActive) != 0 ||
                    Volatile.Read(ref _disposed) != 0 ||
                    generation != Volatile.Read(ref _designerEditGeneration))
                {
                    return;
                }

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

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (_applyingDesignerEdit ||
                Volatile.Read(ref _closeAttemptActive) != 0 ||
                Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            var cancellation = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref _bufferReloadCancellation, cancellation);
            previous?.Cancel();

            _joinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await Task.Delay(250, cancellation.Token);
                    await _joinableTaskFactory.SwitchToMainThreadAsync(cancellation.Token);

                    if (!_applyingDesignerEdit &&
                        Volatile.Read(ref _closeAttemptActive) == 0 &&
                        Volatile.Read(ref _disposed) == 0)
                    {
                        _session.OpenDocument(ReadBuffer(), _documentMoniker);
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

        protected override void OnClose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SynchronizeBeforeClose();
            DisposeResources();
            base.OnClose();
        }

        int IVsWindowFrameNotify3.OnShow(int show)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (show == (int)__FRAMESHOW.FRAMESHOW_WinClosed)
            {
                DisposeResources();
            }
            else if (_lastSelection is not null)
            {
                if (_lastToolboxItems is not null)
                {
                    PopulateToolbox(_lastToolboxItems);
                }

                PublishSelection(_lastSelection);
            }

            return VSConstants.S_OK;
        }

        int IVsWindowFrameNotify3.OnMove(int x, int y, int width, int height) =>
            VSConstants.S_OK;

        int IVsWindowFrameNotify3.OnSize(int x, int y, int width, int height) =>
            VSConstants.S_OK;

        int IVsWindowFrameNotify3.OnDockableChange(
            int dockable,
            int x,
            int y,
            int width,
            int height) =>
            VSConstants.S_OK;

        int IVsWindowFrameNotify3.OnClose(ref uint saveOptions)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SynchronizeBeforeClose();
            return VSConstants.S_OK;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (disposing)
            {
                SynchronizeBeforeClose();
                DisposeResources();
            }

            base.Dispose(disposing);
        }

        private void SynchronizeBeforeClose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Volatile.Read(ref _disposed) != 0 ||
                Interlocked.Exchange(ref _closeAttemptActive, 1) != 0)
            {
                return;
            }

            Interlocked.Increment(ref _designerEditGeneration);
            try
            {
                var cancellation = Interlocked.Exchange(ref _bufferReloadCancellation, null);
                var hasPendingHostBufferEdit = cancellation is not null;
                cancellation?.Cancel();

                string? finalXaml = _control.RequestFinalXaml(TimeSpan.FromSeconds(1));
                if (DesignerSession.ShouldApplyFinalXaml(hasPendingHostBufferEdit, finalXaml))
                {
                    WriteBuffer(finalXaml!);
                }

                cancellation?.Dispose();
                _session.OpenDocument(ReadBuffer(), _documentMoniker);
            }
            finally
            {
                Volatile.Write(ref _closeAttemptActive, 0);
            }
        }

        private void DisposeResources()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Interlocked.Exchange(ref _closeAttemptActive, 1);
            Interlocked.Increment(ref _designerEditGeneration);

            var cancellation = Interlocked.Exchange(ref _bufferReloadCancellation, null);
            cancellation?.Cancel();
            cancellation?.Dispose();

            if (_textBuffer is not null)
            {
                _textBuffer.Changed -= OnTextBufferChanged;
                _textBuffer = null;
            }

            _registeredFrame = null;
            _control.Dispose();
        }
    }
}
