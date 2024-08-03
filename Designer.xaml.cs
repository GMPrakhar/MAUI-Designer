

using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices.Sensors;
using System.Text;

namespace MAUIDesigner;

public partial class Designer : ContentPage
{
    private bool isDragging = false;
    private View? draggingView;
    private View? focusedView;
    private Rectangle? scalerRect;
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
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
    }

    private void CreateElementInDesignerFrame(object sender, TappedEventArgs e)
    {
        try
        {
            var newElement = ElementCreator.Create((sender as Label).Text);
            var tapGestureRecognizer = new TapGestureRecognizer();
            tapGestureRecognizer.Tapped += EnableElementForOperations;
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
    private void EnableElementForOperations(object? sender, TappedEventArgs e)
    {
        var senderView = sender as View;
        draggingView = senderView;
        AddBorder(senderView, null);
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
        gradientBorder2.Opacity = 0;
        if(draggingView != null) return;
    }

    private void AddBorder(object? sender, PointerEventArgs e)
    {
        try
        {
            View? senderView = (sender as View);
            var location = new Point(senderView.X, senderView.Y);

            gradientBorder2.HeightRequest = senderView.Height;
            gradientBorder2.WidthRequest = senderView.Width;
            gradientBorder2.Opacity = 1;
            gradientBorder2.Margin = new Thickness(senderView.X, senderView.Y);
        }catch(Exception)
        {

        }
    }

    private void PointerGestureRecognizer_PointerMoved(object sender, PointerEventArgs e)
    {
        Point location = e.GetPosition(designerFrame).Value;
        //if (focusedView != null && isDragging)
        //{
        //    focusedView.HeightRequest += focusedView.Y - location.Y;
        //    focusedView.WidthRequest += location.X - focusedView.X;
        //    focusedView.Margin = new Thickness(location.X, location.Y);
        //}

        if (!isDragging || draggingView == null) return;

        gradientBorder2.Opacity = 1;
        draggingView.Margin = new Thickness(location.X, location.Y);
        gradientBorder2.Margin = draggingView.Margin;
        gradientBorder2.HeightRequest = draggingView.Height;
        gradientBorder2.WidthRequest = draggingView.Width;
    }

    private void PointerGestureRecognizer_PointerPressed(object sender, PointerEventArgs e)
    {
        isDragging = true;
        Point location = e.GetPosition(designerFrame).Value;

        // Get all children of designerFrame and find the element that is being dragged
        var childElements = designerFrame.Children.Where(c => c.Frame.Contains(location) && c != gradientBorder2);


        var firstChild = (View)childElements.FirstOrDefault();
        if (firstChild is Rectangle)
        {
            scalerRect = firstChild as Rectangle;
            var nonRectFirstChild = childElements.Where(c => c != firstChild).FirstOrDefault();
            if (nonRectFirstChild != null)
            {
                draggingView = nonRectFirstChild as View;
            }
        }
        else
        {
            draggingView = firstChild;
        }

        if (draggingView != null)
        {
            AddBorder(draggingView, null);
            focusedView = draggingView;
            // Get properties for the focused view and display it in the properties panel
            var properties = ToolBox.GetAllPropertiesForView(focusedView);
            Properties.Children.Clear();

            foreach (var property in properties)
            {
                var label = new Label
                {
                    Text = property.Key,
                    FontSize = 10,
                    TextColor = Colors.White,
                    BackgroundColor = Colors.Transparent,
                };
                var value = property.Value;
                // Put the label and value in a grid layout
                var grid = new Grid()
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    HeightRequest = 30,
                    Margin = new Thickness(0, 0, 0, 10),
                    InputTransparent = true,
                    CascadeInputTransparent = false,
                };

                grid.Children.Add(label);
                grid.Children.Add(value);

                grid.SetColumn(label, 0);
                grid.SetColumn(value, 1);


                Properties.Children.Add(grid);
            }
        }
        else
        {
            focusedView = null;
            RemoveBorder(draggingView, null);
        }
    }

    private void PointerGestureRecognizer_PointerReleased(object sender, PointerEventArgs e)
    {
        if (!isDragging || draggingView == null) return;
        isDragging = false;
        //RemoveBorder(draggingView, null);
        draggingView = null;
        scalerRect = null;
    }

    private void DragGestureRecognizer_DragStarting(object? sender, DragStartingEventArgs e)
    {
        scalerRect = sender as Rectangle;
    }

    private void DragGestureRecognizer_DropCompleted(object? sender, DropCompletedEventArgs e)
    {
        scalerRect = null;
    }
}