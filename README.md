# MAUI Designer - Angular

A powerful web-based visual designer for creating MAUI (Microsoft App UI) layouts with drag-and-drop functionality. This Angular application provides an intuitive interface for designing XAML-based user interfaces with real-time preview capabilities.

![MAUI Designer](https://img.shields.io/badge/Angular-18.2.0-red) ![TypeScript](https://img.shields.io/badge/TypeScript-5.5.4-blue) ![License](https://img.shields.io/badge/License-MIT-green)

## 🚀 Features

### Visual Design Tools
- **Drag-and-Drop Interface**: Intuitive visual design with drag-and-drop functionality
- **Element Toolbox**: Comprehensive collection of MAUI controls and layouts
- **Properties Panel**: Real-time property editing with immediate visual feedback
- **Hierarchy Panel**: Tree view of element structure for easy navigation
- **Resizable Panels**: Customizable workspace with adjustable panel sizes

### XAML Integration
- **XAML Editor**: Full-featured code editor with syntax support
- **Real-time Preview**: Instant visual updates when applying XAML changes
- **XAML Generation**: Export designed layouts as clean XAML code
- **XAML Parsing**: Import existing XAML files to recreate visual designs
- **Copy & Download**: Easy sharing and saving of generated XAML

### Supported MAUI Elements

#### Controls
- **Label**: Text display with formatting options
- **Button**: Interactive buttons with styling
- **Entry**: Single-line text input fields
- **Editor**: Multi-line text input areas
- **Image**: Image display with positioning

#### Layouts
- **StackLayout**: Vertical/horizontal stacking of elements
- **Grid**: Row and column-based layouts with spanning
- **AbsoluteLayout**: Precise positioning with coordinates

#### Views
- **Frame**: Containers with borders and backgrounds
- **ScrollView**: Scrollable content areas

## 🛠️ Prerequisites

Before you begin, ensure you have the following installed:

- **Node.js** (version 16.x or higher)
- **npm** (version 8.x or higher)
- **Angular CLI** (version 18.x)

## 📦 Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/GMPrakhar/MAUI-Designer.git
   cd MAUI-Designer
   ```

2. **Install dependencies:**
   ```bash
   npm install
   ```

3. **Install Angular CLI globally (if not already installed):**
   ```bash
   npm install -g @angular/cli
   ```

## 🚀 Getting Started

### Development Server

Start the development server:

```bash
npm start
# or
ng serve
```

Navigate to `http://localhost:4200/` in your browser. The application will automatically reload when you make changes to the source files.

### Building for Production

Build the project for production:

```bash
npm run build
# or
ng build
```

The build artifacts will be stored in the `dist/` directory.

### Running Tests

Execute the unit tests:

```bash
npm test
# or
ng test
```

## 🎯 Usage Guide

### 1. Creating Your First Layout

1. **Select a Layout Container**: Start by adding a layout container (StackLayout, Grid, or AbsoluteLayout) from the toolbox
2. **Add Controls**: Drag controls like Label, Button, or Entry from the toolbox to your layout
3. **Configure Properties**: Use the Properties panel to customize appearance, text, colors, and positioning
4. **Preview XAML**: Check the XAML tab to see the generated code in real-time

### 2. Working with the Toolbox

The toolbox is organized into three categories:

- **Controls**: Interactive elements like buttons, labels, and input fields
- **Layouts**: Containers that organize child elements (StackLayout, Grid, AbsoluteLayout)
- **Views**: Specialized containers like Frame and ScrollView

### 3. Properties Panel

The Properties panel allows you to modify:

- **Layout Properties**: Position (x, y), size (width, height), margins, padding
- **Visual Properties**: Background color, text color, font family, font size
- **Content Properties**: Text content, images, and other element-specific properties
- **Grid Properties**: Row/column position and spanning for Grid layouts

### 4. XAML Editor

- **Apply Changes**: Click the "Apply" button to update the visual design from XAML code
- **Reset**: Revert to the current visual design state
- **Copy**: Copy the generated XAML to clipboard
- **Download**: Save the XAML as a file

### 5. Hierarchy Panel

- View the complete element structure
- Select elements for editing
- Navigate complex layouts easily

## 🏗️ Project Structure

```
src/
├── app/
│   ├── components/           # UI Components
│   │   ├── designer-canvas/  # Main design surface
│   │   ├── hierarchy-panel/  # Element tree view
│   │   ├── properties-panel/ # Property editor
│   │   ├── toolbox/         # Element toolbox
│   │   └── xaml-editor/     # XAML code editor
│   ├── models/              # Data models
│   │   ├── maui-element.ts  # MAUI element definitions
│   │   └── toolbox.ts       # Toolbox item definitions
│   ├── services/            # Business logic services
│   │   ├── drag-drop.ts     # Drag-and-drop functionality
│   │   ├── element.ts       # Element management
│   │   ├── layout-designer.ts # Layout calculations
│   │   ├── xaml-generator.ts # XAML code generation
│   │   └── xaml-parser.ts   # XAML parsing
│   └── app.ts              # Main app component
├── styles.scss             # Global styles
└── index.html             # Main HTML file
```

## 🔧 Development

### Architecture

The application follows Angular's standalone components architecture with a service-based approach:

- **Components**: Each UI panel is a standalone component with its own logic
- **Services**: Business logic is centralized in injectable services
- **Models**: TypeScript interfaces define data structures for MAUI elements
- **Reactive Programming**: Uses RxJS for state management and real-time updates

### Key Services

- **ElementService**: Manages element creation, selection, and hierarchy
- **LayoutDesignerService**: Handles layout calculations and positioning
- **XamlGeneratorService**: Converts visual designs to XAML code
- **XamlParserService**: Parses XAML code into visual elements
- **DragDropService**: Manages drag-and-drop interactions

### Adding New MAUI Elements

1. **Define the element type** in `models/maui-element.ts`
2. **Add toolbox entry** in `models/toolbox.ts`
3. **Implement element creation** in `services/element.ts`
4. **Add XAML generation logic** in `services/xaml-generator.ts`
5. **Update parser** in `services/xaml-parser.ts`

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/your-feature-name`
3. **Make your changes** and ensure they follow the coding standards
4. **Test your changes** thoroughly
5. **Commit your changes**: `git commit -m 'Add some feature'`
6. **Push to the branch**: `git push origin feature/your-feature-name`
7. **Open a Pull Request**

### Development Guidelines

- Follow Angular coding style guidelines
- Write unit tests for new features
- Ensure all existing tests pass
- Update documentation as needed
- Use meaningful commit messages

## 📋 Technology Stack

- **Frontend Framework**: Angular 18.2.0
- **UI Components**: Angular Material 18.2.0
- **Drag & Drop**: Angular CDK 18.2.0
- **Language**: TypeScript 5.5.4
- **Styling**: SCSS
- **State Management**: RxJS 7.8.0
- **Build Tool**: Angular CLI 18.2.0
- **Testing**: Jasmine & Karma

## 🐛 Known Issues

- Build may fail in environments without internet access due to Google Fonts dependency
- Some advanced XAML features are not yet supported
- Complex nested layouts may require manual XAML adjustments

## 📚 Resources

- [MAUI Documentation](https://docs.microsoft.com/en-us/dotnet/maui/)
- [Angular Documentation](https://angular.io/docs)
- [Angular Material](https://material.angular.io/)
- [TypeScript Documentation](https://www.typescriptlang.org/docs/)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👨‍💻 Author

**GMPrakhar** - [GitHub Profile](https://github.com/GMPrakhar)

## 🙏 Acknowledgments

- Microsoft MAUI team for the excellent UI framework
- Angular team for the robust web framework
- Contributors and community members

---

**Happy Designing!** 🎨