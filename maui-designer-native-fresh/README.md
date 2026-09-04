# MAUI Designer Native

A Windows-native visual designer for .NET MAUI XAML. The designer renders real
MAUI controls, discovers framework and extension controls through reflection,
and keeps edits in an immutable document model that supports undo and redo.

## Current capabilities

- Discovers public MAUI, CommunityToolkit, app-local, and externally loaded
  `View` types without a per-control registry.
- Uses assembly-qualified control identities so controls with the same short
  name do not collide.
- Renders a searchable toolbox, hierarchy, native design surface, grouped
  property inspector, and transactional XAML drawer.
- Supports click-to-add, pointer-captured drag/drop with target previews,
  selection, delete, move, resize, reparenting, and bounded undo/redo.
- Matches the web designer's viewport workflow with device presets, 25%-300%
  focal-point zoom, fit/reset, Ctrl+wheel zoom, middle-button or Space+drag
  panning, configurable snap/grid controls, rulers, and light/dark previews.
- Uses extensible layout adapters for `AbsoluteLayout`, measured Grid cells,
  stack insertion positions, generic layouts, and single-content containers.
- Preserves resources, namespaces, markup extensions, attached properties,
  custom-control namespaces, and unknown property elements during XAML
  round-trips.
- Uses binding `FallbackValue` values for design-time previews while retaining
  the original binding expression in generated XAML.
- Loads custom-control assemblies and their adjacent dependencies at runtime.

## Requirements

- Windows 10 version 1809 or later
- .NET 10 SDK
- .NET MAUI Windows workload

```powershell
dotnet workload install maui-windows
```

## Build and test

From this directory:

```powershell
dotnet restore .\MAUIDesigner.Fresh.slnx
dotnet test .\MAUIDesigner.Fresh.Core.Tests\MAUIDesigner.Fresh.Core.Tests.csproj -c Release
dotnet test .\MAUIDesigner.Fresh.App.Tests\MAUIDesigner.Fresh.App.Tests.csproj -c Release -r win-x64 -p:PublishReadyToRun=false
dotnet build .\MAUIDesigner.Fresh.App\MAUIDesigner.Fresh.App.csproj -c Release
```

Run the Debug build:

```powershell
dotnet run --project .\MAUIDesigner.Fresh.App\MAUIDesigner.Fresh.App.csproj -c Debug
```

The app is unpackaged and self-contained for the Windows App SDK.

## Custom controls

Select **Load controls** and choose a control assembly. The loader resolves
dependencies from the assembly directory, registers all constructible public
`View` types, and refreshes the toolbox. Assemblies that publish
`XmlnsDefinitionAttribute` metadata retain their official XAML namespace URI.

Controls requiring constructor arguments can be registered through
`IControlCatalog.RegisterFactory<TView>`.

## XAML workflow

Open **XAML** to edit the current document. **Apply** parses into a temporary
document and only replaces the active design when the entire input is valid.
Errors include source locations and leave the current design unchanged.
**Refresh** regenerates XAML from the current immutable document.

Runtime-only constructs such as live bindings, converters, commands, and
behaviors are retained in XAML but are not executed by the designer. Literal
properties and binding fallback values drive the native preview.

[`Samples/ComplexToolkitPage.xaml`](Samples/ComplexToolkitPage.xaml) is the
native compatibility fixture. It combines page resources, a style, Grid
definitions and attached properties, binding fallback text, a Toolkit
`Expander` named visual slot, `AvatarView`, and a Toolkit behavior.

## DevFlow validation

Debug builds register the MAUI DevFlow agent on port `9223`. The
`.mauidevflow` file contains the local configuration.

```powershell
dotnet tool install --global Microsoft.Maui.Cli
maui devflow ui status
maui devflow ui tree
maui devflow ui screenshot --output designer.png
maui devflow ui diagnostics
```

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for boundaries and interaction budgets,
and [VALIDATION.md](VALIDATION.md) for mutation-based test instrument checks.
`DesignerDocument` is the only source of truth; native controls are disposable
projections, and committed changes flow through `DocumentSession`.
