using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

using IServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// Creates the designer editor for a `.xaml` document. The document data is a
    /// standard text buffer, so the built-in XAML editor and the designer can be
    /// open on the same file at the same time and stay in sync.
    /// </summary>
    [Guid(FactoryGuidString)]
    public sealed class DesignerEditorFactory : IVsEditorFactory, IDisposable
    {
        /// <summary>Editor factory GUID, referenced by the registration attributes.</summary>
        public const string FactoryGuidString = "c1e5aa5a-2f4f-4c92-9b1c-8f5a55da0e77";

        private readonly AsyncPackage _package;
        private ServiceProvider? _serviceProvider;
        private IServiceProvider? _oleServiceProvider;

        public DesignerEditorFactory(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public int SetSite(IServiceProvider serviceProvider)
        {
            _oleServiceProvider = serviceProvider;
            _serviceProvider = new ServiceProvider(serviceProvider);
            return VSConstants.S_OK;
        }

        public int Close() => VSConstants.S_OK;

        public int MapLogicalView(ref Guid logicalView, out string? physicalView)
        {
            physicalView = null;

            // Both the primary and the designer view show the drag and drop surface.
            if (logicalView == VSConstants.LOGVIEWID_Primary || logicalView == VSConstants.LOGVIEWID_Designer)
            {
                return VSConstants.S_OK;
            }

            return VSConstants.E_NOTIMPL;
        }

        public int CreateEditorInstance(
            uint createFlags,
            string documentMoniker,
            string? physicalView,
            IVsHierarchy hierarchy,
            uint itemId,
            IntPtr existingDocData,
            out IntPtr documentView,
            out IntPtr documentData,
            out string editorCaption,
            out Guid commandUiGuid,
            out int createDocumentWindowFlags)
        {
            documentView = IntPtr.Zero;
            documentData = IntPtr.Zero;
            editorCaption = " [Designer]";
            commandUiGuid = Guid.Empty;
            createDocumentWindowFlags = 0;

            if ((createFlags & (uint)(__VSCREATEEDITORFLAGS.CEF_OPENFILE | __VSCREATEEDITORFLAGS.CEF_SILENT)) == 0)
            {
                return VSConstants.E_INVALIDARG;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            // Reuse the buffer the built-in editor already opened, otherwise create one.
            var textLines = existingDocData != IntPtr.Zero
                ? Marshal.GetObjectForIUnknown(existingDocData) as IVsTextLines
                : CreateTextBuffer();

            if (textLines is null)
            {
                // Another editor owns this document with an incompatible buffer type.
                return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
            }

            if (existingDocData == IntPtr.Zero)
            {
                documentData = Marshal.GetIUnknownForObject(textLines);
            }
            else
            {
                documentData = existingDocData;
                Marshal.AddRef(documentData);
            }

            var pane = new DesignerPane(_package, textLines, documentMoniker, hierarchy);
            documentView = Marshal.GetIUnknownForObject(pane);

            return VSConstants.S_OK;
        }

        private IVsTextLines? CreateTextBuffer()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var localRegistry = _serviceProvider?.GetService(typeof(SLocalRegistry)) as ILocalRegistry;
            if (localRegistry is null)
            {
                return null;
            }

            var bufferGuid = typeof(IVsTextLines).GUID;
            var hresult = localRegistry.CreateInstance(
                typeof(VsTextBufferClass).GUID,
                null,
                ref bufferGuid,
                (uint)CLSCTX.CLSCTX_INPROC_SERVER,
                out var buffer);

            if (ErrorHandler.Failed(hresult) || buffer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var textLines = (IVsTextLines)Marshal.GetObjectForIUnknown(buffer);
                if (textLines is IObjectWithSite objectWithSite && _oleServiceProvider is not null)
                {
                    objectWithSite.SetSite(_oleServiceProvider);
                }

                return textLines;
            }
            finally
            {
                Marshal.Release(buffer);
            }
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
            _oleServiceProvider = null;
        }
    }
}
