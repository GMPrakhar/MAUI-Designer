# MAUI Designer

A native visual designer for .NET MAUI, built with **C# and WinUI**. Drag controls
onto the canvas, edit their properties, and generate XAML. The canvas hosts
**real MAUI views**, not HTML/CSS approximations.

The current desktop app lives in [`maui-designer-native/`](maui-designer-native/README.md)
and targets **Windows with .NET 10**.

## Features

- Reflection-based toolbox for MAUI, CommunityToolkit and custom-control assemblies.
- Native layout, drag/drop, resizing, reparenting and hierarchy navigation.
- Multi-selection, clipboard actions, keyboard commands and undo/redo.
- Grouped property editing, Grid track editors, and direct row/column/span placement.
- Device presets, zoom, pan, snap/grid controls and rulers in a light workspace.
- Revision-acknowledged live XAML sync that reconnects after transport interruptions.
- XAML round-trips preserve resources, styles, templates, visual states, namespaces,
  custom attributes, and markup extensions.
- A separate real-MAUI runtime preview and DevFlow inspection in Debug builds.

## Getting started

**Requirements:** Windows 10 version 1809 or later, the .NET 10 SDK,
and the MAUI Windows workload.

```sh
git clone https://github.com/GMPrakhar/MAUI-Designer.git
cd MAUI-Designer
dotnet workload install maui-windows
dotnet run --project maui-designer-native/MAUIDesigner.Fresh.App/MAUIDesigner.Fresh.App.csproj -c Debug
```

No browser, Node.js or Angular development server is needed for the native app.

Native release builds are published as `MAUIDesigner-win-x64.zip` by the
`native-v*` release workflow. Look for **MAUI Designer** native prereleases on
the [releases page](https://github.com/GMPrakhar/MAUI-Designer/releases), rather
than the separate VSIX download.

## Designing a page

1. Drag a control or layout from the toolbox onto the canvas.
2. Select it on the canvas or in the hierarchy, then resize it or edit its properties.
3. Open **XAML** to edit the document. Valid edits update the canvas automatically;
   invalid input leaves the current design unchanged.

Use **Load controls** to add custom-control assemblies and their adjacent dependencies.
Runtime-only bindings, converters, commands and behaviors are preserved in XAML
but are not executed by the designer; literal values and binding fallbacks drive previews.

## Visual Studio extension

The [VSIX](extension/README.md) hosts the **native designer** inside a Visual
Studio document tab and synchronizes the existing XAML text buffer. It is not
a WebView-based editor.

Download `MauiDesigner.vsix` from the
[VSIX release](https://github.com/GMPrakhar/MAUI-Designer/releases/download/vsix-latest/MauiDesigner.vsix),
close Visual Studio, install it, then choose **Open With > MAUI Designer** on a
`.xaml` file. Requires Windows and Visual Studio 2022 or 2026.

## Development and related projects

Build the native app on Windows:

```sh
dotnet build maui-designer-native/MAUIDesigner.Fresh.App/MAUIDesigner.Fresh.App.csproj -c Release
```

See the [native development guide](maui-designer-native/README.md) for publishing
and platform details. Follow [CONTRIBUTING.md](CONTRIBUTING.md): integrate feature
work on `develop`, then promote approved changes to `main` through a pull request.

The earlier Angular app remains in `src/` as a separate implementation; its
HTML/CSS preview is not the native designer described here.

## Support

Created by [GMPrakhar](https://github.com/GMPrakhar).

**Buy me a coffee!**

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.me/gmprakhar)

## License

[GNU GPL v3.0 only](LICENSE.md). Dependencies retain their own licenses.
Earlier README revisions stated MIT, then PolyForm Noncommercial; this does
not retroactively withdraw rights granted under those statements.
