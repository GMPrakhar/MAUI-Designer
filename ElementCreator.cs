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

    internal class ActivityIndicatorFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new ActivityIndicator
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                IsRunning = true
            };
        }
    }

    internal class IndicatorViewFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new IndicatorView
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20
            };
        }
    }

    internal class BorderFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Border
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                StrokeShape = new Rectangle()
            };
        }
    }

    internal class BoxViewFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new BoxView
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20
            };
        }
    }

    internal class ButtonFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new Button
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 44,
                Text = "Click me!"
            };
        }
    }

    internal class CheckBoxFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new CheckBox
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 44
            };
        }
    }

    internal class DatePickerFactory : ElementFactory
    {
        public override View CreateElement()
        {
            return new DatePicker
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 44,
                MinimumWidthRequest = 44
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
            { nameof(ActivityIndicator), new ActivityIndicatorFactory() },
            { nameof(IndicatorView), new IndicatorViewFactory() },
            { nameof(Border), new BorderFactory() },
            { nameof(BoxView), new BoxViewFactory() },
            { nameof(Button), new ButtonFactory() },
            { nameof(CheckBox), new CheckBoxFactory() },
            { nameof(DatePicker), new DatePickerFactory() },
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

            throw new ArgumentException($"Element type '{elementTypeName}' is not supported.");
        }
    }
}