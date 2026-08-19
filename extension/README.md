# MAUI Designer for Visual Studio

Visual Studio 2022 has no drag-and-drop designer for .NET MAUI XAML. This folder
packages the Angular designer from this repository as a VSIX so it can edit the
`.xaml` file that is open in the IDE, with the project's own NuGet controls in
the toolbox.

See [`../docs/visual-studio-extension.md`](../docs/visual-studio-extension.md)
for the design rationale and the alternatives that were considered.

## Layout

| Project | Framework | Builds on |
| --- | --- | --- |
| `src/MauiDesigner.Core` | `netstandard2.0` | any OS |
| `tests/MauiDesigner.Core.Tests` | `net8.0` | any OS |
| `tests/Fakes/*` | `netstandard2.0` | any OS |
| `tests/MauiDesigner.Vsix.CompileCheck` | `net472` | any OS |
| `src/MauiDesigner.Vsix` | `net472` | **Windows only** (packaging) |

`MauiDesigner.Core.sln` contains everything that is cross-platform and is what CI
builds and tests. `MauiDesigner.sln` additionally contains the VSIX project itself
and requires Visual Studio 2022 with the *Visual Studio extension development*
workload.

Only *packaging* the VSIX needs Windows — the VSSDK build tools ship with Visual
Studio. The extension's **source compiles anywhere**, and
`MauiDesigner.Vsix.CompileCheck` does exactly that: it compiles the same `.cs`
files against the real Visual Studio SDK reference assemblies, so CI catches a
wrong interface, signature or enum on Linux instead of at F5 on someone's
machine. It is part of `MauiDesigner.Core.sln`, so `dotnet build` covers it.

This is also why `DesignerControl` builds its UI in code instead of in XAML: XAML
markup compilation is Windows-only, and the UI is one WebView plus a status
label.

## What the core library does

* **`Projects/ProjectAssetsReader`** — reads `obj/project.assets.json` (written by
  every NuGet restore) to find which packages a project references and where
  their assemblies live in the global packages folder.
* **`Manifests/ControlManifestGenerator`** — inspects those assemblies with
  `MetadataLoadContext` (metadata only, no code is executed), finds public
  concrete types deriving from `Microsoft.Maui.Controls.View`, reads their
  `public static readonly BindableProperty XxxProperty` fields and emits the same
  manifest JSON the designer already consumes for custom controls.
* **`Protocol/DesignerSession`** — the host half of the message contract in
  `src/app/services/host-bridge.ts`, free of any Visual Studio types so it can be
  unit tested anywhere.

### Message contract

| Direction | Message | Meaning |
| --- | --- | --- |
| host → designer | `host.ready` | which IDE is hosting, and the open file |
| host → designer | `document.load` | XAML to edit |
| host → designer | `manifests.push` | controls found in the project's packages |
| host → designer | `document.saved` | the document reached disk |
| designer → host | `designer.ready` | the WebView finished booting |
| designer → host | `document.changed` | the user edited the design |
| designer → host | `document.save` | Ctrl+S inside the designer |
| designer → host | `manifests.request` | asks for the project's controls |
| designer → host | `designer.error` | something went wrong, for the output window |

Both sides ignore malformed payloads, so a protocol mismatch degrades to "the
designer does nothing" rather than taking down the IDE.

## Building and running the tests

Cross-platform (this is what CI runs):

```bash
cd extension
dotnet test MauiDesigner.Core.sln
```

The tests build two fake assemblies — a stand-in `Microsoft.Maui.Controls` and a
`Contoso.Maui.Controls` control package — so the reflection scanner is exercised
for real without installing the MAUI workload.

That command also compiles the extension sources through the compile-check
project. To verify it is doing its job, change a VS interface implementation (say
drop the `ref` from `IVsEditorFactory.MapLogicalView`) and the build fails.

The VSIX, on Windows:

```powershell
npm ci
npm run build            # produces dist/angular-designer, embedded in the VSIX
cd extension
msbuild MauiDesigner.sln /p:Configuration=Release /restore
```

`bin\Release\MauiDesigner.Vsix.vsix` can then be installed, or press F5 to debug
in the experimental instance. Building without running `npm run build` first is a
hard error: a VSIX that installs but renders an empty WebView is a worse failure
than one that refuses to build.

You do not need a Windows machine to get an installer, though —
`.github/workflows/release-vsix.yml` packages the VSIX on a `windows-latest`
runner. It runs on every pull request that touches `extension/`, unzips the
result and asserts that `webview/index.html` and both assemblies are actually
inside, then uploads it as a build artifact. Pushing a `vsix-v*` tag publishes
the same file as a pre-release asset named `MauiDesigner.vsix`, which is what the
website's download link points at.

## How it works inside Visual Studio

`MauiDesignerPackage` registers `DesignerEditorFactory` for `.xaml` at a *lower*
priority than the built-in XAML editor, so double-clicking a file keeps the
familiar behaviour and the designer is offered through **Open With…**.

The factory reuses the document's existing `IVsTextLines` buffer when one is
already open, which means the text editor and the designer edit the same buffer:
changes made on the canvas appear in the XAML view immediately, undo/redo and the
dirty indicator keep working, and Ctrl+S saves through the normal solution
pipeline.

`DesignerControl` hosts WebView2. The compiled Angular application is copied into
`webview\` inside the VSIX and served through
`SetVirtualHostNameToFolderMapping`, because a `file://` origin cannot use the
storage and module loading the designer relies on. The WebView2 environment is
created explicitly with a user data folder under `%LOCALAPPDATA%` — the Visual
Studio install directory is read-only.

## Limitations

* **Visual Studio does not run on Linux or macOS** — Visual Studio is a Windows
  product, and Visual Studio for Mac was retired in August 2024. Installing the
  VSIX and clicking through the designer therefore has to happen on Windows; what
  CI can prove on Linux is that everything compiles against the real SDK and that
  the protocol and manifest logic behave correctly.
* Only Visual Studio 2022 (17.x). The out-of-process `VisualStudio.Extensibility`
  model cannot host WebView2, so the classic in-process VSSDK model is required.
* The designer understands the subset of XAML the web app supports; unknown tags
  are preserved verbatim but are not editable beyond their attributes.
* Manifest generation reads compile-time metadata, so a control's runtime
  defaults are not known — the designer falls back to its own defaults.
