

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices.Sensors;
using System.Text;

namespace MAUIDesigner;

public partial class Designer : ContentPage
{
    private bool isDragging = false;
    private View? draggingView;
    Border gradientBorder = new Border
    {
        StrokeThickness = 2,
        ZIndex = -10,
        Padding = new Thickness(20,0),
        HorizontalOptions = LayoutOptions.Center,
        StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(2)
        },
        Stroke = new SolidColorBrush(Colors.White),
        InputTransparent = true,
        IsEnabled = false
    };
    private const string LabelXAML = "<Label Text=\"Drag Me away\" HorizontalTextAlignment=\"Center\" TextColor=\"White\" FontSize=\"36\">\r\n</Label>";
    private IDictionary<Guid, View> views = new Dictionary<Guid, View>();

    public Designer()
	{
		InitializeComponent();
        var allVisualElements = ToolBox.GetAllVisualElementsAlongWithType();
        foreach (var element in allVisualElements)
        {
            var label = new Label
            {
                Text = element.Key,
                FontSize = 20,
                TextColor = Colors.White,
                BackgroundColor = Colors.Transparent,
                Margin = new Thickness(10),
            };
            var gestureRecognizer = new TapGestureRecognizer();
            gestureRecognizer.Tapped += CreateElementInDesignerFrame;
            label.GestureRecognizers.Add(gestureRecognizer);
            Toolbox.Children.Add(label);
        }
        designerFrame.Add(gradientBorder);
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
    }

    private void CreateElementInDesignerFrame(object sender, TappedEventArgs e)
    {
        try
        {
            var newElement = ElementCreator.Create((sender as Label).Text);
            var gestureRecognizer = new PointerGestureRecognizer();
            gestureRecognizer.PointerEntered += AddBorder;
            gestureRecognizer.PointerExited += RemoveBorder;
            newElement.GestureRecognizers.Add(gestureRecognizer);
            designerFrame.Add(newElement);
            views.Add(newElement.Id, newElement);

            // Get the XAML from the new element
            var xaml = GetXamlForElement(designerFrame);
        }
        catch (Exception et)
        {
            Console.WriteLine(et);
            // Do nothing
        }
    }


    public string GetXamlForElement(VisualElement element)
    {
        StringBuilder xamlBuilder = new StringBuilder();
        xamlBuilder.AppendLine($"<{element.GetType().Name}");

        foreach (var property in element.GetType().GetProperties())
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                var value = property.GetValue(element);
                var valueType = value?.GetType();
                if (value != null && OnlyPrimitiveTypeOrString(valueType))
                {
                    xamlBuilder.AppendLine($"    {property.Name}=\"{value}\"");
                }
            }
        }

        // Check if element can contain children
        if (element is Layout layout)
        {
            xamlBuilder.AppendLine(">");
            foreach (var child in layout.Children.Where(x => x is VisualElement))
            {
                xamlBuilder.AppendLine(GetXamlForElement(child as VisualElement));
            }
            xamlBuilder.AppendLine($"</{element.GetType().Name}>");
        }
        else
        {

            xamlBuilder.AppendLine("/>");
        }
        return xamlBuilder.ToString();
    }

    private bool OnlyPrimitiveTypeOrString(Type valueType)
    {
        // Check if the type is a primitive type or string
        return valueType.IsPrimitive || valueType == typeof(string);
    }

    private void RemoveBorder(object? sender, PointerEventArgs e)
    {
        gradientBorder.Opacity = 0;
        if(draggingView != null) return;
    }

    private void AddBorder(object? sender, PointerEventArgs e)
    {
        try
        {
            if (draggingView != null) return;
            View? senderView = (sender as View);
            var location = new Point(senderView.X, senderView.Y);

            gradientBorder.HeightRequest = senderView.Height;
            gradientBorder.WidthRequest = senderView.Width;
            gradientBorder.Opacity = 1;
            gradientBorder.Margin = new Thickness(senderView.X, senderView.Y);
        }catch(Exception)
        {

        }
    }

    private void PointerGestureRecognizer_PointerMoved(object sender, PointerEventArgs e)
    {
        if(!isDragging || draggingView == null) return;

        Point location = e.GetPosition(designerFrame).Value;
        gradientBorder.Opacity = 1;
        draggingView.Margin = new Thickness(location.X, location.Y);
        gradientBorder.Margin = draggingView.Margin;
        gradientBorder.HeightRequest = draggingView.Height;
        gradientBorder.WidthRequest = draggingView.Width;
    }

    private void PointerGestureRecognizer_PointerPressed(object sender, PointerEventArgs e)
    {
        isDragging = true;
        Point location = e.GetPosition(designerFrame).Value;

        // Get all children of designerFrame and find the element that is being dragged
        var childElements = designerFrame.Children.Where(c => c.Frame.Contains(location) && c is not Border);

        draggingView = (View)childElements.FirstOrDefault();
    }

    private void PointerGestureRecognizer_PointerReleased(object sender, PointerEventArgs e)
    {
        if (!isDragging || draggingView == null) return;
        isDragging = false;
        RemoveBorder(draggingView, null);
        draggingView = null;
    }


}