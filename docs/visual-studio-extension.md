# Converting MAUI Designer into a Visual Studio extension

*Design rationale — verified against Microsoft Learn, WebView2 and VS Code extension docs.*

> **Status: Route A is implemented.** The VSIX and its cross-platform core library live in
> [`extension/`](../extension/README.md); this document explains why it is built the way it is, and
> what the alternatives were. Route B (VS Code) is still open.

## Short answer

**Yes, and it fills a real gap.** Visual Studio 2022 has **no drag-and-drop XAML designer for .NET MAUI**,
and Microsoft has stated a drag-and-drop UI designer "is not part of our direction for .NET MAUI"
([Developer Community](https://developercommunity.visualstudio.com/t/XAML-Designer-for-Net-MAUI/10224319)).
What ships instead is XAML Hot Reload and XAML Live Preview (design-time Live Preview arrived in
17.14) — read-only mirrors of a running app, not layout editing.

Two hosting routes are viable today. Both reuse this Angular app essentially unchanged, because the
designer core (parser, generator, element/alignment/clipboard services, custom-control registry) is
pure TypeScript with no server dependency.

| Route | Feasible today | Rough effort | Trade-off |
| --- | --- | --- | --- |
| **VS Code extension** — `CustomTextEditorProvider` + webview | ✅ | ~10–15 dev-days | Cross-platform, simple file APIs; .NET type introspection is awkward from Node |
| **VS 2022 VSIX** — classic VSSDK + WebView2 + `IVsEditorFactory` | ✅ | ~18–28 dev-days | Deepest integration and real NuGet/type discovery; COM-heavy, Windows-only |
| **VisualStudio.Extensibility (out-of-process)** | ❌ | — | Remote UI is WPF-XAML-over-RPC with no WebView2, and custom document editors are not supported out-of-process yet |

Recommended sequencing: **ship the VS Code extension first, then the VS 2022 VSIX** on the same
Angular core.

## Route A — VS 2022 VSIX

### Hosting the app
Only the **classic in-process VSSDK** model can host web content. A WPF `UserControl` containing
`Microsoft.Web.WebView2.Wpf.WebView2` is placed in a `ToolWindowPane` (or an editor pane).

```csharp
var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
await webView.EnsureCoreWebView2Async(env);          // never set Source/CreationProperties in XAML
webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
    "maui-designer.local", Path.Combine(installDir, "WebApp"),
    CoreWebView2HostResourceAccessKind.Allow);
webView.CoreWebView2.Navigate("https://maui-designer.local/index.html");
```

`SetVirtualHostNameToFolderMapping` is the right way to serve the Angular `dist/` folder — `file://`
is blocked, and a local HTTP server is unnecessary. Ship `dist/` as VSIX `Content`.

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
4. Emit the same manifest schema the designer already consumes and push it into the webview.

The manifest format documented in the README is deliberately the contract for exactly this.

### Main risks
- Synchronising three states — the Angular canvas, the `IVsTextBuffer`, and VS's own undo stack — is
  the hardest problem; the app's undo history is independent of VS's.
- WPF "airspace" glitches when VS popups overlap WebView2 (mitigate with `WebView2CompositionControl`).
- The WebView2 Runtime must be present (ships with Windows 11/Edge, not guaranteed on locked-down machines).
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

## What changes in this repository

Reusable unchanged: `xaml-parser.ts`, `xaml-generator.ts`, `element.ts`, `alignment.ts`,
`clipboard.ts`, `drag-drop.ts`, `layout-designer.ts`, `custom-control-registry.ts`, and every
component template.

Needs work:

| Area | Change |
| --- | --- |
| New `host-bridge.service.ts` | ~50 lines abstracting `chrome.webview.postMessage` (VS) vs `acquireVsCodeApi()` (VS Code) vs standalone browser |
| Save/load | Route through the host bridge instead of `localStorage` and browser download/upload |
| Custom control registry | Accept manifests pushed by the host (generated from `PackageReference`s) alongside imported and bundled ones |
| `angular.json` | Use a relative `baseHref` (`./`) so the bundle works under a virtual host |
| `index.html` | VS Code only: nonce injection point |

A separate companion repository is the cleanest home for the C#/TypeScript host, consuming this
project's `dist/` output as a build artifact.

## Key references

- [Out-of-process extensibility model overview](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/get-started/oop-extensibility-model-overview?view=visualstudio) and [Remote UI](https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/inside-the-sdk/remote-ui?view=visualstudio)
- [Creating custom editors and designers](https://learn.microsoft.com/en-us/visualstudio/extensibility/creating-custom-editors-and-designers?view=visualstudio), [Supporting multiple document views](https://learn.microsoft.com/en-us/visualstudio/extensibility/supporting-multiple-document-views?view=visualstudio)
- [WebView2 in WPF](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf), [`SetVirtualHostNameToFolderMapping`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping)
- [NuGet API in Visual Studio](https://learn.microsoft.com/en-us/nuget/visual-studio-extensibility/nuget-api-in-visual-studio), [MetadataLoadContext](https://learn.microsoft.com/en-us/dotnet/standard/assembly/inspect-contents-using-metadataloadcontext)
- [VS Code custom editors](https://code.visualstudio.com/api/extension-guides/custom-editors) and [webviews](https://code.visualstudio.com/api/extension-guides/webview)
- [XAML Live Preview enhancements for .NET MAUI](https://devblogs.microsoft.com/visualstudio/enhancements-to-xaml-live-preview-in-visual-studio-for-net-maui/)
