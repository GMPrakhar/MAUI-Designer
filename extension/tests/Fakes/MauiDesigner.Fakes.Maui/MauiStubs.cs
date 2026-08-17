namespace Microsoft.Maui.Graphics
{
    public class Color
    {
    }
}

namespace Microsoft.Maui.Controls
{
    public sealed class BindableProperty
    {
        public static BindableProperty Create(string name) => new BindableProperty();
    }

    public abstract class Element
    {
    }

    public abstract class VisualElement : Element
    {
    }

    public abstract class View : VisualElement
    {
    }

    public abstract class Layout : View
    {
    }
}
