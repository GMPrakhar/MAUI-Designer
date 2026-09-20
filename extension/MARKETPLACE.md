# MAUI Designer

MAUI Designer is a native WYSIWYG editor for .NET MAUI XAML pages, integrated
directly into Visual Studio.

The designer renders real MAUI controls in an isolated process while keeping the
Visual Studio XAML buffer synchronized in both directions. Revision
acknowledgments, retry, reconnection, and stale-edit rejection protect edits when
the host or designer is busy.

## Highlights

- Native MAUI rendering rather than an HTML approximation.
- Live visual-to-XAML and XAML-to-visual editing.
- Grid row, column, row-span, and column-span editing.
- Preservation of resources, styles, templates, visual states, attached
  properties, custom namespaces, and unknown attributes.
- Drag and drop, reparenting, hierarchy navigation, multi-selection, marquee
  selection, resize handles, and keyboard editing commands.
- Zoom, pan, device presets, rulers, snapping, and grid guides.
- CommunityToolkit.Maui and compatible custom-control discovery.

## Use the designer

1. Install the VSIX and restart Visual Studio.
2. Open a .NET MAUI solution.
3. Right-click a `.xaml` page in Solution Explorer and select **Open With...**.
4. Select **MAUI Designer**, then select **Open**.

The standard Visual Studio XAML editor remains available through **Open With...**.

## Requirements

- Windows 10 or Windows 11.
- Visual Studio 2022 or Visual Studio 2026.
- A .NET MAUI project.

The extension includes its self-contained native designer backend.

## Feedback

Report reproducible problems and unsupported XAML patterns at
[github.com/GMPrakhar/MAUI-Designer/issues](https://github.com/GMPrakhar/MAUI-Designer/issues).
Include your Visual Studio, .NET, and MAUI versions and the smallest XAML sample
that reproduces the issue.
