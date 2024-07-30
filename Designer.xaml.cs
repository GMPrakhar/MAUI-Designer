

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
        designerFrame.Add(gradientBorder);
    }
    //private async void DragGestureRecognizer_DragStarting_1(object sender, DragStartingEventArgs e)
    //{
    //    var label = (sender as Element)?.Parent as VisualElement;
    //    await label.TranslateTo(10000, 10000, 1);
    //    e.Data.Properties.Add("DraggingObject", label);
    //}

    //private void DropGestureRecognizer_Drop_1(object sender, DropEventArgs e)
    //{
    //    var data = e.Data.Properties["DraggingObject"] as VisualElement;
    //    // add the data element as a child of the frame

    //    //frameGrid.Add(data);

    //    Point location = e.GetPosition(data).Value;

    //    data.TranslateTo(location.X + data.TranslationX + data.X - data.Width/2, location.Y + data.TranslationY - data.Y, 1);

    //    data.Opacity = 1;
    //}

    //private async void ElementSelector_DragStarted(object sender, DragStartingEventArgs e)
    //{
    //    var element = sender as VisualElement;
    //    // Duplicate element as a VisualElement class object
    //    /*var elementType = element.GetType();
    //    VisualElement duplicate = Activator.CreateInstance(elementType) as VisualElement;
    //    element.GetType().GetProperties().ToList().ForEach(p =>
    //    {
    //        if (p.CanWrite)
    //        {
    //            p.SetValue(duplicate, p.GetValue(element));
    //        }
    //    });*/

    //    //Point location = e.GetPosition(element).Value;
    //    await element.TranslateTo(10000, 10000, 1);

    //    e.Data.Properties.Add("DraggingObject", element);
    //}

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
            //var gestureRecognizer = new PointerGestureRecognizer();
            //gestureRecognizer.PointerExited += RemoveBorder;

            //gradientBorder.GestureRecognizers.Add(gestureRecognizer);

            gradientBorder.HeightRequest = senderView.Height;
            gradientBorder.WidthRequest = senderView.Width+10;
            gradientBorder.Opacity = 1;
            gradientBorder.TranslationX = senderView.TranslationX+ gradientBorder.Width/4;
            gradientBorder.TranslationY = senderView.Y;
            gradientBorder.Frame = new Rect(senderView.X, senderView.Y, senderView.Width, senderView.Height);
        }catch(Exception)
        {

        }
    }

    private void PointerGestureRecognizer_PointerMoved(object sender, PointerEventArgs e)
    {
        // Get pointer location from PointerEventArgs
        if(!isDragging || draggingView == null) return;

        Point location = e.GetPosition(designerFrame).Value;
        gradientBorder.Opacity = 1;
        gradientBorder.ZIndex = -10;
        draggingView.ZIndex = 0;
        draggingView.TranslationX = location.X - draggingView.Width/3;
        draggingView.TranslationY = location.Y - draggingView.Height;
        gradientBorder.TranslationX = location.X;
        gradientBorder.TranslationY = location.Y;
        draggingView.Frame = new Rect(location.X, location.Y, draggingView.Width, draggingView.Height);
        gradientBorder.Frame = new Rect(location.X, location.Y + draggingView.Height, draggingView.Width, draggingView.Height);
    }

    private void PointerGestureRecognizer_PointerPressed(object sender, PointerEventArgs e)
    {
        isDragging = true;
        Point location = e.GetPosition(designerFrame).Value;
        ShowPointer.Text = location.ToString();
        // Get all children of designerFrame and find the element that is being dragged
        var childElements = designerFrame.Children.Where(c => c.Frame.Contains(location) && c is not Border);

        draggingView = (View)childElements.FirstOrDefault();
        if (draggingView != null)
        {
           // gradientBorder.HeightRequest = draggingView.Height;
           // gradientBorder.WidthRequest = draggingView.Width;
        }
    }

    private void PointerGestureRecognizer_PointerReleased(object sender, PointerEventArgs e)
    {
        if (!isDragging || draggingView == null) return;
        isDragging = false;
        RemoveBorder(draggingView, null);
        //Point location = e.GetPosition(designerFrame).Value;
        //await draggingView.TranslateTo(location.X, location.Y - 80, 1);
        //views[draggingView.Id].Layout(new Rect(location.X, location.Y, draggingView.Width, draggingView.Height));
        //draggingView.Layout(new Rect(location.X, location.Y, draggingView.Width, draggingView.Height));
        draggingView = null;
    }


}