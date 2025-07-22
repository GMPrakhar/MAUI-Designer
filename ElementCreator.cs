using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace MAUIDesigner
{
    internal abstract class ElementFactory
    {
        public abstract View CreateElement();
    }

    internal class LabelFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Label
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                Text = "Drag me!"
            };
        }
    }

    internal class EditorFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Editor
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                Text = "Type here",
                IsEnabled = true
            };
        }
    }

    internal class LayoutFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new StackLayout
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                HeightRequest = 50,
                WidthRequest = 50,
                BackgroundColor = Colors.Coral
            };
        }
    }

        internal class RectangleFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Rectangle
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                HeightRequest = 50,
                WidthRequest = 100
            };
        }
    }

    internal class RoundRectangleFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new RoundRectangle
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                HeightRequest = 60,
                WidthRequest = 120,
                CornerRadius = new CornerRadius(10),
                Fill = new SolidColorBrush(Colors.LightBlue)
            };
        }
    }


    internal class ButtonFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Button
            {
                Margin = new Thickness(10),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                Text = "Click me!"
            };
        }
    }

    internal class EllipseFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Ellipse
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 88
            };
        }
    }

    internal class LineFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Line
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 88
            };
        }
    }

    internal class EntryFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Entry
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 44,
                Placeholder = "Enter text"
            };
        }
    }

    internal class FrameFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Frame
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 44,
                HasShadow = false
            };
        }
    }

    internal class ImageButtonFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new ImageButton
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 44
            };
        }
    }

    internal static class ElementCreator
    {
        private static readonly Dictionary<string, ElementFactory> factories = new()
        {
            { nameof(Label), new LabelFactory() },
            { nameof(Editor), new EditorFactory() },
            { nameof(StackLayout), new LayoutFactory() },
            { nameof(Rectangle), new RectangleFactory() },
            { nameof(RoundRectangle), new RoundRectangleFactory() },
            { nameof(Button), new ButtonFactory() },
            { nameof(Ellipse), new EllipseFactory() },
            { nameof(Line), new LineFactory() },
            { nameof(Entry), new EntryFactory() },
            { nameof(Frame), new FrameFactory() },
            { nameof(ImageButton), new ImageButtonFactory() }
        };

        internal static View Create(string elementTypeName)
        {
            if (string.IsNullOrWhiteSpace(elementTypeName))
            {
                System.Diagnostics.Debug.WriteLine("ElementCreator: Null or empty element type name provided");
                return CreateFallbackElement("Invalid element name");
            }

            // Try factory-based creation first (optimized path)
            if (factories.TryGetValue(elementTypeName, out var factory))
            {
                try
                {
                    var element = factory.CreateElement();
                    System.Diagnostics.Debug.WriteLine($"ElementCreator: Successfully created {elementTypeName} using factory");
                    return element;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ElementCreator: Factory creation failed for {elementTypeName}: {ex.Message}");
                    // Continue to reflection-based creation
                }
            }

            // Try reflection-based creation with improved error handling
            try
            {
                var elementType = FindElementType(elementTypeName);
                if (elementType != null)
                {
                    var instance = CreateInstanceWithValidation(elementType);
                    if (instance is View view)
                    {
                        SetDefaultProperties(view);
                        System.Diagnostics.Debug.WriteLine($"ElementCreator: Successfully created {elementTypeName} using reflection");
                        return view;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ElementCreator: Reflection creation failed for {elementTypeName}: {ex.Message}");
            }

            // Fallback to error element
            System.Diagnostics.Debug.WriteLine($"ElementCreator: All creation methods failed for {elementTypeName}, creating fallback");
            return CreateFallbackElement($"Failed to create: {elementTypeName}");
        }

        private static Type? FindElementType(string elementTypeName)
        {
            // Search in Microsoft.Maui.Controls assembly
            var mauiTypes = typeof(View).Assembly.GetTypes()
                .Where(t => t.Name == elementTypeName && 
                           (t.IsSubclassOf(typeof(View)) || t.IsSubclassOf(typeof(Layout))))
                .ToArray();

            if (mauiTypes.Length == 1)
                return mauiTypes[0];

            // If multiple types found, prefer non-abstract ones
            var concreteTypes = mauiTypes.Where(t => !t.IsAbstract).ToArray();
            if (concreteTypes.Length > 0)
                return concreteTypes[0];

            // Search in Microsoft.Maui.Controls.Shapes assembly for shapes
            var shapeTypes = typeof(Microsoft.Maui.Controls.Shapes.Shape).Assembly.GetTypes()
                .Where(t => t.Name == elementTypeName && t.IsSubclassOf(typeof(View)))
                .ToArray();

            return shapeTypes.FirstOrDefault(t => !t.IsAbstract);
        }

        private static object? CreateInstanceWithValidation(Type elementType)
        {
            // Check if type has a parameterless constructor
            var constructor = elementType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                System.Diagnostics.Debug.WriteLine($"ElementCreator: No parameterless constructor found for {elementType.Name}");
                return null;
            }

            // Create instance
            return Activator.CreateInstance(elementType);
        }

        private static void SetDefaultProperties(View view)
        {
            // Set common default properties for better usability
            try
            {
                if (view.GetType().GetProperty("Margin") != null)
                {
                    view.Margin = new Thickness(10);
                }

                if (view.GetType().GetProperty("MinimumHeightRequest") != null)
                {
                    view.MinimumHeightRequest = 20;
                }

                if (view.GetType().GetProperty("MinimumWidthRequest") != null)
                {
                    view.MinimumWidthRequest = 20;
                }

                // Set specific defaults for text-based controls
                if (view is Label label && string.IsNullOrEmpty(label.Text))
                {
                    label.Text = "New Label";
                }
                else if (view is Button button && string.IsNullOrEmpty(button.Text))
                {
                    button.Text = "New Button";
                }
                else if (view is Entry entry && string.IsNullOrEmpty(entry.Placeholder))
                {
                    entry.Placeholder = "Enter text";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ElementCreator: Error setting default properties: {ex.Message}");
                // Continue even if default property setting fails
            }
        }

        private static View CreateFallbackElement(string errorMessage)
        {
            return new Label 
            { 
                Text = errorMessage,
                TextColor = Colors.Red,
                FontSize = 10,
                Margin = new Thickness(10),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 100,
                BackgroundColor = Color.FromRgba(255, 200, 200, 100)
            };
        }

        /// <summary>
        /// Gets all available element types that can be created
        /// </summary>
        internal static IEnumerable<string> GetAvailableElementTypes()
        {
            var factoryTypes = factories.Keys;
            var reflectionTypes = typeof(View).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(View)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => t.Name);

            return factoryTypes.Concat(reflectionTypes).Distinct().OrderBy(name => name);
        }
    }
}