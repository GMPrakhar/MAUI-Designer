# MAUI Designer
MAUI Designer is a .NET 8.0 MAUI application for designing XAML-based user interfaces with drag-and-drop functionality. It provides a visual XAML editor, element toolbox, properties panel, and real-time preview capabilities for creating MAUI UI layouts.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Platform Requirements - CRITICAL
- **Windows ONLY**: This application targets `net8.0-windows10.0.19041.0` exclusively and uses Windows-specific dependencies
- **DO NOT attempt to build on Linux or macOS** - it will fail due to missing Windows assemblies (Microsoft.UI, UWP compatibility)
- **Requires Windows 10 version 19041 or later** for development and runtime

### Prerequisites and Setup
- Install .NET 8.0 SDK: Download from https://dot.net/v1/dotnet-install.ps1 and run on Windows
- Install MAUI workloads: `dotnet workload install maui` -- takes 3-5 minutes. NEVER CANCEL. Set timeout to 10+ minutes.
- Verify installation: `dotnet workload list` should show maui workloads installed

### Building the Application
- **Bootstrap and build**:
  - `cd /path/to/MAUI-Designer`
  - `dotnet restore` -- takes 30-60 seconds for package restoration
  - `dotnet build` -- takes 2-5 minutes depending on system. NEVER CANCEL. Set timeout to 10+ minutes.
- **Build for specific configuration**:
  - Debug: `dotnet build --configuration Debug`
  - Release: `dotnet build --configuration Release`

### Running the Application
- **Run in debug mode**: `dotnet run` or press F5 in Visual Studio
- **Windows Machine profile**: Configured in Properties/launchSettings.json for MSIX packaging
- The application will launch as a Windows desktop application with the XAML designer interface

## Validation
- **Manual Testing Required**: After making changes, always test these core scenarios:
  1. **XAML Editor**: Write XAML in the text editor and click "Render" to see the preview
  2. **Drag and Drop**: Select elements from the toolbox and drag them to the design surface
  3. **Element Scaling**: Select an element and use border anchors to resize it  
  4. **Properties Panel**: Select an element and modify properties like color, margin, padding, text
  5. **XAML Generation**: Use the "Generate XAML" feature to export the designed layout
  6. **Load Design**: Import existing XAML to recreate the visual design
- **AbsoluteLayout Focus**: Currently only supports AbsoluteLayout as the base container
- **No Automated Tests**: This repository has no test infrastructure - all validation must be manual
- **Post-Build Validation**: Always run the application after building to ensure UI components load correctly
- **Designer Workflow Test**: Create a simple layout with Label, Button, and Entry controls to verify basic functionality

### Build Time Expectations
- **First build**: 5-10 minutes (includes package downloads). NEVER CANCEL. Set timeout to 15+ minutes.
- **Incremental builds**: 1-3 minutes. NEVER CANCEL. Set timeout to 5+ minutes.
- **Clean rebuild**: 3-7 minutes. NEVER CANCEL. Set timeout to 10+ minutes.

## Cross-Platform Limitations
- **Linux/macOS**: Cannot build or run due to Windows-specific dependencies:
  - Microsoft.UI.Input and Microsoft.UI.Xaml assemblies
  - UWP compatibility platform requirements
  - Windows-specific cursor and UI element APIs
- **Android/iOS**: While MAUI supports these platforms, this specific project uses Windows-only APIs
- **DO NOT modify TargetFrameworks** - the app architecture requires Windows-specific features

## Repository Structure

### Key Projects and Files
- **MAUIDesigner.csproj**: Main project file targeting net8.0-windows10.0.19041.0
- **Designer.xaml/.cs**: Main designer interface with toolbox, properties, and canvas
- **ElementCreator.cs**: Factory for creating MAUI UI elements (343 lines)
- **ToolBox.cs**: Element selection and drag-and-drop functionality (445 lines) 
- **PropertyManager.cs**: Property editing and binding system (150 lines)

### Important Directories
- **LayoutDesigners/**: AbsoluteLayout, GridLayout, VerticalStackLayout designers
- **DnDHelper/**: Drag-and-drop operations, scaling, and hover behaviors
- **HelperViews/**: Context menus, element designer views, tab management
- **XamlHelpers/**: XAML generation and color conversion utilities
- **Services/**: Cursor, gesture, and tab setup services
- **Platforms/**: Platform-specific code for Android, iOS, Windows, MacCatalyst, Tizen

### Common Commands Reference
```
# Repository root contents
ls -la
App.xaml                 # Application definition
Designer.xaml            # Main designer interface  
ElementCreator.cs         # UI element factory
ToolBox.cs               # Toolbox functionality
PropertyManager.cs        # Property editing
MAUIDesigner.csproj      # Project file
MAUIDesigner.sln         # Solution file
README.md                # Project documentation

# Package dependencies (from MAUIDesigner.csproj)
CommunityToolkit.Maui (9.0.3)
Microsoft.Maui.Controls
Microsoft.Maui.Controls.Compatibility  
Microsoft.Extensions.Logging.Debug (8.0.0)
AathifMahir.Maui.MauiIcons.Fluent (3.0.0)
```

## Development Workflow
- **Always work on Windows**: Use Visual Studio 2022 or VS Code with C# Dev Kit
- **Test all UI changes**: The application is a visual designer - always verify UI functionality
- **Focus on XAML designer workflow**: Test element creation, property editing, and layout generation
- **No linting required**: No specific linting tools configured
- **No CI/CD**: Repository has no GitHub Actions or automated build pipeline

## Troubleshooting
- **Build fails with "EnableWindowsTargeting"**: You're not on Windows - this app requires Windows
- **Missing MAUI workload**: Run `dotnet workload install maui` and wait for completion
- **XAML compilation errors**: Ensure Windows SDK components are installed
- **UI elements not rendering**: Check that all Windows-specific dependencies are available
- **Package restore failures**: Delete bin/ and obj/ folders, then run `dotnet restore` again
- **Long build times**: First builds include package downloads - expect 5-10 minutes initially

## Additional Information
- **Repository size**: ~1550 lines of C# code across main files
- **No custom build scripts**: Uses standard dotnet CLI commands
- **Target audience**: MAUI developers creating Windows desktop applications
- **Project maturity**: Active development, no formal release versioning yet