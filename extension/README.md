# MAUI Designer for Visual Studio

Visual Studio 2022 has no drag-and-drop designer for .NET MAUI XAML. This folder
packages the native MAUI Designer as a VSIX companion process. Visual Studio
hosts its HWND inside the document tab and synchronizes the shared `.xaml` text
buffer over a private named pipe; no MAUI assemblies are loaded into `devenv`.

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

This is also why `DesignerControl` builds its UI in code instead of in XAML:
the in-process surface is only an `HwndHost` plus a status label.

## What can be verified without Visual Studio

Visual Studio has never run on Linux (VS Code is a different product, and Visual
Studio for Mac was retired in August 2024), so the VSIX can never be *installed*
on a Linux agent. Rather less obviously, most of the ways this extension can be
wrong are still catchable there:

| Failure | Caught by | How |
| --- | --- | --- |
| Wrong SDK interface, signature or enum | `MauiDesigner.Vsix.CompileCheck` | Compiles the real sources against the real VS SDK |
| Deadlocks and shutdown races | the same project | `VSTHRD*`/`VSSDK*` analyzers, escalated to errors |
| Registration that silently never loads | `RegistrationMetadataTests` | Reads the compiled attributes with `MetadataLoadContext` |
| A manifest naming a file that isn't there | `RegistrationMetadataTests` | Resolves every path the manifest references |
| A package that installs but renders nothing | `release-vsix.yml` | Unzips the built VSIX and asserts its contents |
| Host protocol drift | Native bridge tests | Exchanges ready/load/change messages through a real named pipe |
| A VSIX that won't install, or installs without registering | `release-vsix.yml` (`install-check`) | Installs it into a real Visual Studio on a Windows runner |

The last row is the one that needs a Windows machine, and CI supplies two: the
`windows-2022` image ships Visual Studio 2022 and `windows-latest` now ships
Visual Studio 2026, both with the extension development workload. The
`install-check` job runs against **both**, because a supported-version range is
only a promise until something installs against it — ours claimed `[17.0,18.0)`
and so excluded Visual Studio 2026 entirely, which this job caught on its first
run. VSIXInstaller had exited `0` while deploying nothing, so "the installer
succeeded" is not on its own evidence of anything.

Each run installs with the real `VSIXInstaller.exe`, asserts the **deployed**
`.pkgdef` associates the editor factory with `.xaml`, runs
`devenv /updateconfiguration` so Visual Studio parses that `.pkgdef` itself, then
reads back VS's *private registry hive* to confirm what it actually stored — the
editor factory, its owning package, the `.xaml` association and its priority, and
the designer logical view. The two checks answer different questions: the
`.pkgdef` is generated from the registration attributes, so it catches attributes
that compile happily while describing the wrong editor, but it is still only what
we asked for. Only the hive shows what Visual Studio accepted. It then uninstalls
and checks nothing is left behind. Publishing a release is gated on that job, so
a VSIX that cannot install can never be shipped.

Two things about that hive are easy to get wrong, and both cost real runs here.
Visual Studio merges `.pkgdef` registrations into
`Software\Microsoft\VisualStudio\<version>_<instance>`**`_Config`**, not the bare
`<version>_<instance>` key, which holds user settings — read the wrong one and
you get a null that looks exactly like a rejected registration. And the hive must
be opened with `RegLoadAppKey`, the API Visual Studio itself uses: `reg load`
fails with *Access is denied* because the IDE's background service hosts still
hold the file.

What still needs a Windows UI run: seeing the child process render inside the
IDE and checking focus, resize, docking, and DPI transitions.

The threading analyzers are worth singling out. They flag exactly the bugs that
otherwise need a running IDE to find — and they found real ones here: the pane
was posting the user's designer edits through `ThreadHelper.JoinableTaskFactory`,
whose tasks explicitly **do not** block Visual Studio from exiting, so an unlucky
shutdown could have dropped an edit on its way to the text buffer. Both it and
`DesignerControl` now use the `AsyncPackage`'s factory instead. Note that
`FileAndForget` alone does *not* fix this; see
[VSSDK007](https://github.com/Microsoft/VSSDK-Analyzers/blob/main/doc/VSSDK007.md).

What still genuinely requires Windows: installing the VSIX, confirming **Open
With… → MAUI Designer** appears, and validating the embedded native window.

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
| host → designer | `host.close` | flush final valid XAML for a correlated close attempt |
| designer → host | `designer.ready` | the native process connected |
| designer → host | `document.changed` | the user edited the design |
| designer → host | `document.save` | Ctrl+S inside the designer |
| designer → host | `manifests.request` | asks for the project's controls |
| designer → host | `designer.error` | something went wrong, for the output window |
| designer → host | `designer.closed` | final valid XAML for the matching close request is ready |

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
dotnet workload install maui-windows
cd extension
msbuild MauiDesigner.sln /p:Configuration=Release /restore
```

`bin\Release\MauiDesigner.Vsix.vsix` can then be installed, or press F5 to debug
in the experimental instance. The build publishes a self-contained win-x64
native backend and packages it under `native\`. Set
`MAUI_DESIGNER_NATIVE_PATH` to use a local executable while debugging the VSIX.

You do not need a Windows machine to get an installer, though —
`.github/workflows/release-vsix.yml` packages the VSIX on a `windows-latest`
runner. It runs on every pull request that touches `extension/`, unzips the
result and asserts that `native/MAUIDesigner.exe` and both assemblies are actually
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

`DesignerControl` creates a child Win32 host, starts the packaged
`MAUIDesigner.exe`, and reparents its window into the editor pane. A uniquely
named pipe carries newline-delimited protocol messages. A reversible pre-close
handshake flushes edits before Visual Studio's save prompt; only the committed
`FRAMESHOW_WinClosed` notification closes the pipe and terminates the process
owned by that pane, so canceling the prompt leaves the designer usable.

## Limitations

* **Visual Studio does not run on Linux or macOS** — Visual Studio is a Windows
  product, and Visual Studio for Mac was retired in August 2024. Installing the
  VSIX and clicking through the designer therefore has to happen on Windows; what
  CI can prove on Linux is that everything compiles against the real SDK and that
  the protocol and manifest logic behave correctly.
* Windows only, Visual Studio 2022 and 2026 (17.x and 18.x). A small classic
  VSSDK shim is still required because the newer extensibility model does not
  expose custom document editors or arbitrary native child-window hosting.
* Win32 cross-process parenting requires compatible DPI-awareness modes. The
  extension and MAUI backend both use per-monitor-aware Windows UI stacks.
* Manifest generation reads compile-time metadata, so a control's runtime
  defaults are not known — the designer falls back to its own defaults.

## Licence

[GNU GPL v3](../LICENSE.md), the same as the rest of the repository. The
manifest points Visual Studio at it, so the terms are shown before the
extension installs rather than buried in a file nobody opens.
