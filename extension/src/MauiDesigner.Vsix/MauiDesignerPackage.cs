using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

using Task = System.Threading.Tasks.Task;

namespace MauiDesigner.Vsix
{
    /// <summary>
    /// Registers the MAUI designer as an alternative editor for `.xaml` files.
    /// </summary>
    /// <remarks>
    /// The priority is deliberately lower than the built-in XAML editor so the
    /// designer never steals the default double-click experience; it is offered
    /// through <c>File &gt; Open With</c> and the context menu instead.
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideEditorFactory(typeof(DesignerEditorFactory), 110, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorExtension(typeof(DesignerEditorFactory), ".xaml", 0x10, NameResourceID = 110)]
    [ProvideEditorLogicalView(typeof(DesignerEditorFactory), VSConstants.LOGVIEWID.Designer_string)]
    public sealed class MauiDesignerPackage : AsyncPackage
    {
        /// <summary>Package GUID, referenced by the generated pkgdef.</summary>
        public const string PackageGuidString = "3b9a7bd5-51b1-4f2a-9a1d-0f0a4a2f9c31";

        /// <inheritdoc />
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            RegisterEditorFactory(new DesignerEditorFactory(this));
        }
    }
}
