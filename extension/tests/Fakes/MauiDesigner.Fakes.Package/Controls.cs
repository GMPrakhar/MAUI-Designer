using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Contoso.Maui.Controls
{
    public enum RatingShape
    {
        Star,
        Heart
    }

    public class RatingBar : View
    {
        public static readonly BindableProperty ValueProperty = BindableProperty.Create("Value");
        public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create("IsReadOnly");
        public static readonly BindableProperty CaptionProperty = BindableProperty.Create("Caption");
        public static readonly BindableProperty ShapeProperty = BindableProperty.Create("Shape");
        public static readonly BindableProperty FillColorProperty = BindableProperty.Create("FillColor");

        // Not a BindableProperty field: must be ignored
        public static readonly string Documentation = "https://contoso.example";

        public double Value { get; set; }

        public bool IsReadOnly { get; set; }

        public string? Caption { get; set; }

        public RatingShape Shape { get; set; }

        public Color? FillColor { get; set; }
    }

    public class CardPanel : Layout
    {
        public static readonly BindableProperty TitleProperty = BindableProperty.Create("Title");

        public string? Title { get; set; }
    }

    public abstract class AbstractControl : View
    {
    }

    internal class InternalControl : View
    {
    }

    public class NotAControl
    {
    }
}

namespace Contoso.Maui.Charts
{
    public class SparkLine : Microsoft.Maui.Controls.View
    {
    }
}
