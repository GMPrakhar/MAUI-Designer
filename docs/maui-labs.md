# .NET MAUI Labs and this designer

[dotnet/maui-labs](https://github.com/dotnet/maui-labs) is Microsoft's experimental playground
for MAUI. It is **not** a visual designer. Useful pieces for this repository:

| Lab | What it is | Use for MAUI Designer Native |
| --- | --- | --- |
| **DevFlow** | Inspector / automation for a *running* MAUI app: visual tree, screenshots, property get/set, MCP | Companion to this native designer. The canvas already hosts real `View` instances; DevFlow can verify what the runtime actually laid out. |
| **Cli** | `dotnet tool` (`maui …`) that talks to the DevFlow broker | `maui devflow broker start` then tree / screenshot. |
| **Comet** | Experimental MVU UI toolkit | Not a designer foundation. |
| **Go** | Experimental Go bindings | Out of scope. |
| **WPF / AppKit backends** | Alternate renderers | Not a path to a XAML designer. |
| **AI Extensions / Essentials.AI** | AI helpers | Unrelated to WYSIWYG. |

## DevFlow vs this designer vs the web app

- **Angular designer** (`src/`): parses MAUI XAML and approximates it with HTML/CSS. Fast to iterate, not pixel-true.
- **Native designer** (`maui-designer-native/`): a MAUI WinUI app that **wraps real MAUI controls**. What you see is what MAUI rendered.
- **DevFlow**: inspects that live tree. It does not place, resize, or emit XAML.

The public comment that “this isn’t actually rendering MAUI controls” is true of the web
designer and **false** of the native app.

## Wiring DevFlow (after .NET 10)

The DevFlow agent package currently targets **.NET 10**. This native app is **net8** so the
package is not referenced yet. When the TFM moves:

```csharp
#if DEBUG
builder.AddMauiDevFlowAgent();
#endif
```

```sh
dotnet tool install -g Microsoft.Maui.Cli --prerelease
maui devflow broker start
# inspector: http://localhost:19223/inspector/
```

Keep the agent out of Release builds.

## What we will not take from maui-labs

Comet, Go, WPF/AppKit, and AppProjectReference do not replace a designer. DevFlow is the
only lab that improves WYSIWYG *verification* of this native app.
