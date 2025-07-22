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
            if (factories.TryGetValue(elementTypeName, out var factory))
            {
                return factory.CreateElement();
            }

            try
            {
                var elementType = typeof(View).Assembly.GetTypes().FirstOrDefault(t => t.Name == elementTypeName);
                if (elementType != null)
                {
                    return Activator.CreateInstance(elementType) as View ?? new Label { Text = "Error creating element" };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating element {elementTypeName}: {ex.Message}");
            }

            // Fallback to a basic label if element creation fails
            return new Label { Text = $"Unknown element: {elementTypeName}" };
        }
    }
}