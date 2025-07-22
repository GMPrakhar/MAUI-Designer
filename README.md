
# MAUI Designer

A small project to be able to preview XAML changes into MAUI view, and later add drag and drop functionality to create MAUI UI.

## Current capabilities:
- Write XAML into the text editor, click on Render button, and corresponding view will be rendered in the frame.
- Select views from toolbox and perform drag+drop across the right side frame. Currently only supports AbsoluteLayout.
- Scale the views using anchors on the border of the view.
- Properties menu for selected element so that the properties can be updated and applied directly to the view, including color, margin, padding and text/font manipulations.
- Generate XAML from the design, and load design from XAML. Currently only supports Absolute Layout as the base element ( after ContentPage ).
- **NEW**: Interactive Preview Mode - View your generated XAML as a live, interactive UI in a separate page.

## New Interactive Preview Feature

The MAUI Designer now includes an "Interactive Preview" mode that allows you to:

1. **Run Interactive Mode**: Click the "Run Interactive Mode" button in the Designer title bar
2. **Live Preview**: See your designed UI rendered as an actual interactive MAUI page
3. **Navigation**: Easily navigate back to the designer to make changes
4. **Real-time Updates**: Generate XAML and preview how it will look and behave in a real application

### How to Use Interactive Preview:
1. Design your UI using the drag-and-drop designer
2. Click the "Run Interactive Mode" button in the title bar
3. Your designed UI will open in a new page where you can interact with it
4. Use the "← Back" button to return to the designer

## Architecture Improvements

The codebase has been refactored for better maintainability:

### Core Modules:
- **Core/Elements**: Clean element creation and management with factory pattern
  - `IElementFactory`: Interface for creating UI elements
  - `IElementOperations`: Interface for element operations
  - `ElementService`: Main service for element management
  
- **Core/Properties**: Organized property management system
  - `IPropertyService`: Interface for property operations
  - `PropertyService`: Implementation with category-based organization

### Benefits:
- Cleaner separation of concerns
- Better testability with dependency injection
- Modular architecture for easier maintenance
- Type-safe interfaces for all operations

## Current UI
![image](https://github.com/user-attachments/assets/5e728838-6d0c-4648-b13e-f09e748ae886)

## Designer
![image](https://github.com/user-attachments/assets/f8c0437b-0be7-483f-8f2d-1ed8a4c42322)

## XAML Generator
![image](https://github.com/user-attachments/assets/64cbd604-ce17-4ead-868e-8b3398878ab6)

## Scale Example
![{68091961-CD0D-4969-A94D-FF8B42C41FB3}](https://github.com/user-attachments/assets/7cb46ccb-5ccc-4d81-b81b-0f18ee53cd3c)

## Load example
![image](https://github.com/user-attachments/assets/a003ce9c-3ea1-4083-8cea-259b33fc1aa6)




