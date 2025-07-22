using Microsoft.Maui.Controls.Shapes;

namespace MAUIDesigner.Core.Elements
{
    /// <summary>
    /// Factory for creating Label elements
    /// </summary>
    public class LabelElementFactory : IElementFactory
    {
        public string DisplayName => "Label";
        public string Category => "Controls";

        public View CreateElement()
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

    /// <summary>
    /// Factory for creating Editor elements
    /// </summary>
    public class EditorElementFactory : IElementFactory
    {
        public string DisplayName => "Editor";
        public string Category => "Controls";

        public View CreateElement()
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

    /// <summary>
    /// Factory for creating StackLayout elements
    /// </summary>
    public class StackLayoutElementFactory : IElementFactory
    {
        public string DisplayName => "StackLayout";
        public string Category => "Layouts";

        public View CreateElement()
        {
            return new StackLayout
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                BackgroundColor = Colors.LightGray
            };
        }
    }

    /// <summary>
    /// Factory for creating Button elements
    /// </summary>
    public class ButtonElementFactory : IElementFactory
    {
        public string DisplayName => "Button";
        public string Category => "Controls";

        public View CreateElement()
        {
            return new Button
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                Text = "Click me!",
                IsEnabled = true
            };
        }
    }

    /// <summary>
    /// Factory for creating Entry elements
    /// </summary>
    public class EntryElementFactory : IElementFactory
    {
        public string DisplayName => "Entry";
        public string Category => "Controls";

        public View CreateElement()
        {
            return new Entry
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                Text = "Enter text",
                IsEnabled = true
            };
        }
    }

    /// <summary>
    /// Factory for creating Grid elements
    /// </summary>
    public class GridElementFactory : IElementFactory
    {
        public string DisplayName => "Grid";
        public string Category => "Layouts";

        public View CreateElement()
        {
            return new Grid
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                BackgroundColor = Colors.LightBlue
            };
        }
    }

    /// <summary>
    /// Factory for creating Rectangle elements
    /// </summary>
    public class RectangleElementFactory : IElementFactory
    {
        public string DisplayName => "Rectangle";
        public string Category => "Shapes";

        public View CreateElement()
        {
            return new Rectangle
            {
                Margin = new Thickness(20),
                MinimumHeightRequest = 20,
                MinimumWidthRequest = 20,
                Fill = Brush.Purple,
                WidthRequest = 100,
                HeightRequest = 100
            };
        }
    }
}