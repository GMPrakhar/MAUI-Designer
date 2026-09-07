# Converting MAUI Designer into a Visual Studio extension

*Design rationale for the out-of-process native host.*

> **Status: the native out-of-process route is implemented.** The VSIX and its
> cross-platform protocol library live in [`extension/`](../extension/README.md).

## Short answer

**Yes, and it fills a real gap.** Visual Studio 2022 has **no drag-and-drop XAML designer for .NET MAUI**,
and Microsoft has stated a drag-and-drop UI designer "is not part of our direction for .NET MAUI"
([Developer Community](https://developercommunity.visualstudio.com/t/XAML-Designer-for-Net-MAUI/10224319)).
What ships instead is XAML Hot Reload and XAML Live Preview (design-time Live Preview arrived in
17.14) — read-only mirrors of a running app, not layout editing.

The supported Visual Studio route keeps MAUI outside `devenv`: a self-contained
native process renders real controls, while a small classic VSSDK editor owns
the text buffer and embeds the process window.

| Route | Feasible today | Rough effort | Trade-off |
| --- | --- | --- | --- |
| **VS Code extension** — `CustomTextEditorProvider` + webview | ✅ | ~10–15 dev-days | Cross-platform, simple file APIs; .NET type introspection is awkward from Node |
| **VS 2022/2026 VSIX** — classic VSSDK + native child process | ✅ | Implemented | Real MAUI backend, crash isolation, shared VS text buffer; Windows-only |
| **VisualStudio.Extensibility (out-of-process)** | ❌ | — | Remote UI is WPF-XAML-over-RPC with no WebView2, and custom document editors are not supported out-of-process yet |

The modern `VisualStudio.Extensibility` model remains unsuitable for this part:
it does not provide custom document editors or arbitrary native child windows.

## Route A — VS 2022 VSIX

### Hosting the app

`DesignerControl` contains a WPF `HwndHost`. It starts the packaged
`native\MAUIDesigner.exe` with a unique pipe name, waits for its WinUI top-level
window, changes that window to `WS_CHILD`, and reparents it into the document
tab. Layout changes call `SetWindowPos`, so docking and tab resizing flow to the
MAUI window. Closing the tab terminates its owned process.

The process boundary is also the workload boundary: Visual Studio loads only
the net472 VSIX and netstandard protocol assemblies. MAUI 10, WinUI, Toolkit,
and third-party control code remain in the self-contained child process.

### Editing real `.xaml` files
- `IVsEditorFactory.CreateEditorInstance` + `IVsPersistDocData` + `IVsWindowPane` register a designer
  view for `.xaml`.
- `[ProvideEditorExtension(..., ".xaml", priority)]` with a **lower priority than the built-in XAML
  editor** so the designer shows up under *Open With* instead of hijacking the default.
- Sharing the `VsTextBuffer` with the text editor gives the classic *Design | XAML* split view.

### NuGet-aware custom controls
This is where a VS extension beats the web app: the current JSON manifests could be **generated
automatically** from the project.

1. Read `PackageReference` items from the `.csproj`, or use `IVsPackageInstallerServices`.
2. Resolve each package to its DLLs via `obj/project.assets.json`.
3. Enumerate types with `System.Reflection.MetadataLoadContext` (load-only, no execution) or the
   Roslyn `VisualStudioWorkspace` compilation; keep types deriving from
   `Microsoft.Maui.Controls.View` and their static `BindableProperty` fields.
4. Emit the designer manifest schema and send it to the native process over the private pipe.

The manifest format documented in the README is deliberately the contract for exactly this.

### Main risks
- Synchronising native designer state, the `IVsTextBuffer`, and VS's undo stack
  remains the hardest boundary; messages are ordered over one duplex pipe.
- Pane shutdown uses a bounded, request-correlated `host.close` /
  `designer.closed` handshake so the final canvas state reaches the shared
  buffer before Visual Studio's save decision. Irreversible process cleanup
  waits for `FRAMESHOW_WinClosed`, so a canceled save prompt leaves the pane
  synchronized and usable.
- Cross-process `SetParent` requires compatible DPI-awareness modes.
- A child-process crash must be surfaced in the editor without affecting VS.
- Classic VSSDK is maintained but is not where Microsoft is investing; the modern out-of-process
  model cannot host this app yet.

## Route B — VS Code extension

Register a custom editor for `.xaml` with `"priority": "option"` and host the same bundle in a webview:

```jsonc
"contributes": {
  "customEditors": [{
    "viewType": "mauiDesigner.xamlEditor",
    "displayName": "MAUI Designer",
    "selector": [{ "filenamePattern": "*.xaml" }],
    "priority": "option"
  }]
}
```

XAML flows in via `webview.postMessage` and back via `acquireVsCodeApi().postMessage`, with the
extension applying a `WorkspaceEdit` to the `TextDocument`.

Constraints:
- VS Code enforces a strict CSP, so `index.html` must be templated to add a `nonce` to every
  `<script>` (Angular 16+ also supports `ngCspNonce`).
- The extension host is Node.js, so .NET reflection is not directly available. Control metadata must
  come from a spawned .NET helper, a pre-generated manifest, or the Roslyn language server.

## Implementation map

The classic VSSDK shim is under `extension/src/MauiDesigner.Vsix`. The shared,
testable protocol and project-inspection code is under
`extension/src/MauiDesigner.Core`. The native pipe client is under
`maui-designer-native/MAUIDesigner.Fresh.App/Hosting`. The VSIX build publishes
the native app self-contained and packages it beneath `native/`.

## Key references

- [Out-of-process extensibility model overview](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/get-started/oop-extensibility-model-overview?view=visualstudio) and [Remote UI](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/inside-the-sdk/remote-ui?view=visualstudio)
- [Creating custom editors and designers](https://learn.microsoft.com/en-us/visualstudio/extensibility/creating-custom-editors-and-designers?view=visualstudio), [Supporting multiple document views](https://learn.microsoft.com/en-us/visualstudio/extensibility/supporting-multiple-document-views?view=visualstudio)
- [`HwndHost`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndhost) and [`SetParent`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)
- [NuGet API in Visual Studio](https://learn.microsoft.com/en-us/nuget/visual-studio-extensibility/nuget-api-in-visual-studio), [MetadataLoadContext](https://learn.microsoft.com/en-us/dotnet/standard/assembly/inspect-contents-using-metadataloadcontext)
- [VS Code custom editors](https://code.visualstudio.com/api/extension-guides/custom-editors) and [webviews](https://code.visualstudio.com/api/extension-guides/webview)
- [XAML Live Preview enhancements for .NET MAUI](https://devblogs.microsoft.com/visualstudio/enhancements-to-xaml-live-preview-in-visual-studio-for-net-maui/)
