# MAUI Designer Native

A **.NET MAUI WinUI** designer that hosts **real MAUI controls** on the canvas. This is
the restored C# designer (from `legacy-dotnet-designer`), not the Angular HTML/CSS
approximation in the repository root.

Licence: GNU GPL v3 (same as the rest of the repo).

## Why this exists

The web designer is a MAUI-XAML-aware layout tool. It is useful, but it does not run the
MAUI renderers. This app does: every toolbox item is a `Microsoft.Maui.Controls.View`
wrapped in `ElementDesignerView`.

[DevFlow](../docs/maui-labs.md) (from [dotnet/maui-labs](https://github.com/dotnet/maui-labs))
is the companion inspector for a running MAUI process. It is not a designer.

## Requirements

- Windows 10 17763+ / Windows 11
- Visual Studio 2022 with the **.NET MAUI** workload, or
  `dotnet workload install maui-windows`
- .NET 8 SDK

This app cannot be launched on Linux. CI builds it on `windows-latest`.

## Run

```sh
dotnet workload install maui-windows
dotnet build maui-designer-native/MAUIDesigner.csproj -c Debug -f net8.0-windows10.0.19041.0
dotnet run --project maui-designer-native/MAUIDesigner.csproj -f net8.0-windows10.0.19041.0
```

Unpackaged publish (used by the GitHub Action):

```sh
dotnet publish maui-designer-native/MAUIDesigner.csproj -c Release -f net8.0-windows10.0.19041.0 -p:WindowsPackageType=None
```

## Capabilities

- Toolbox of MAUI views, layouts and shapes (drag onto the canvas)
- Real MAUI layout: AbsoluteLayout, Grid (row/column on drop), StackLayout
- Resize handles (corners and edges) with a live size badge
- Properties panel grouped by category (including `Auto` / `*` / absolute grid lengths)
- Hierarchy tree with expand/collapse and selection
- XAML generate / apply round-trip (AbsoluteLayout root after `ContentPage`)
- Resizable chrome: toolbox, properties, XAML editor

## Known limits

- Windows-only host (Win32 cursor + WinUI drag). Mobile TFMs are not built.
- XAML load expects `ContentPage` → `AbsoluteLayout` as the root pair.
- DevFlow agent is documented, not packaged, until the app moves to .NET 10.
- No automated UI tests on Linux; Windows CI compiles and publishes the unpackaged app.

## Version

`0.2.0` — restored into `maui-designer-native/`, crash fixes, dark chrome, embedded icon map.
