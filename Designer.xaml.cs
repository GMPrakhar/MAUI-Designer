

using MAUIDesigner.XamlHelpers;
using Microsoft.Maui.Controls.Shapes;
namespace MAUIDesigner;

public partial class Designer : ContentPage
{
    private bool isDragging = false;
    private View? focusedView;
    private Rectangle? scalerRect;
    private ISet<Type> nonTappableTypes = new HashSet<Type> { typeof(Editor) };
    private IList<View> nonTappableViews = new List<View>();
    private const string LabelXAML = "<Label Text=\"Drag Me away\" HorizontalTextAlignment=\"Center\" TextColor=\"White\" FontSize=\"36\">\r\n</Label>";
    private IDictionary<Guid, View> views = new Dictionary<Guid, View>();
    private SortedDictionary<string, Grid>? PropertiesForFocusedView;

    public Designer()
	{
		InitializeComponent();
        var allVisualElements = ToolBox.GetAllVisualElementsAlongWithType();
        foreach (var element in allVisualElements)
        {
            var label = new Label
            {
                Text = element.Key,
                FontSize = 10,
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
        // Check if the event location has a child editor element on the location
        var location = e.GetPosition(designerFrame).Value;

        focusedView = nonTappableViews.FirstOrDefault(x => x.Frame.Contains(location));

        if (focusedView != null)
        {
            AddBorder(focusedView, null);
            PopulatePropertyGridField();
            UpdateActualPropertyView();
        }
        else
        {
            RemoveBorder(focusedView, null);
        }
    }

    private void UpdateActualPropertyView()
    {
        Properties.Children.Clear();
        if (PropertiesForFocusedView == null) return;
        foreach (var property in PropertiesForFocusedView)
        {
            Properties.Children.Add(property.Value);
        }
    }

    private void CreateElementInDesignerFrame(object sender, TappedEventArgs e)
    {
        try
        {
            var newElement = ElementCreator.Create((sender as Label).Text);
            newElement.PropertyChanged += ElementPropertyChanged;
            var tapGestureRecognizer = new TapGestureRecognizer();
            tapGestureRecognizer.Tapped += EnableElementForOperations;
            tapGestureRecognizer.Buttons = ButtonsMask.Primary | ButtonsMask.Secondary;
            newElement.GestureRecognizers.Add(tapGestureRecognizer);
            designerFrame.Add(newElement);
            views.Add(newElement.Id, newElement);

            if(nonTappableTypes.Contains(newElement.GetType()))
            {
                nonTappableViews.Add(newElement);
            }

            // Get the XAML from the new element
            var xaml = XAMLGenerator.GetXamlForElement(designerFrame);
        }
        catch (Exception et)
        {
            Console.WriteLine(et);
            // Do nothing
        }
    }

    private void ElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(sender == focusedView)
        {
            RemoveBorder(sender, null);
            AddBorder(sender, null);
            UpdatePropertyForFocusedView(e.PropertyName, focusedView.GetType().GetProperty(e.PropertyName)?.GetValue(focusedView));
        }
    }

    private void EnableElementForOperations(object? sender, TappedEventArgs e)
    {
        var senderView = sender as View;
        focusedView = senderView;
        AddBorder(senderView, null);
        PopulatePropertyGridField();
        UpdateActualPropertyView();
    }

    private void RemoveBorder(object? sender, PointerEventArgs e)
    {
        gradientBorder2.Opacity = 0;
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

        if (!isDragging || focusedView == null) return;

        gradientBorder2.Opacity = 1;
        // Update margin property for the focusedView using Update function
        location.X = (int)location.X;
        location.Y = (int)location.Y;
        UpdatePropertyForFocusedView("Margin", new Thickness(location.X, location.Y));
    }

    private async void PointerGestureRecognizer_PointerPressed(object sender, PointerEventArgs e)
    {
        isDragging = true;
    }

    private void PointerGestureRecognizer_PointerReleased(object sender, PointerEventArgs e)
    {
        isDragging = false;
    }

    private void DragGestureRecognizer_DragStarting(object? sender, DragStartingEventArgs e)
    {
        scalerRect = sender as Rectangle;
    }

    private void DragGestureRecognizer_DropCompleted(object? sender, DropCompletedEventArgs e)
    {
        scalerRect = null;
    }

    private void GenerateXamlForTheView(object sender, EventArgs e)
    {
        var xaml = XAMLGenerator.GetXamlForElement(designerFrame);
        Properties.Clear();

        // create a new label with text as xaml and add it to the Properties
        var label = new Editor
        {
            Text = xaml,
            FontSize = 10,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
        };
        Properties.Children.Add(label);
    }

    private void PopulatePropertyGridField()
    {
        if (focusedView == null) return;
        this.PropertiesForFocusedView?.Clear();
        var properties = ToolBox.GetAllPropertiesForView(focusedView);
        var gridList = new SortedDictionary<string, Grid>();
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

            gridList[property.Key] = grid;
        }

        this.PropertiesForFocusedView = gridList;
    }

    private void UpdatePropertyForFocusedView(string propertyName, object updatedValue)
    {
        if (focusedView == null || PropertiesForFocusedView == null || !PropertiesForFocusedView.ContainsKey(propertyName)) return;
        var property = PropertiesForFocusedView?[propertyName];
        var value = property?.Children[1];
        if (value is Entry entry)
        {
            entry.Text = updatedValue.ToString();
        }
        else if (value is Picker picker)
        {
            picker.SelectedItem = updatedValue;
        }
        else if (value is Grid colorGrid)
        {
            var red = colorGrid.Children[0] as Entry;
            var green = colorGrid.Children[1] as Entry;
            var blue = colorGrid.Children[2] as Entry;
            var alpha = colorGrid.Children[3] as Entry;

            if (updatedValue.GetType() == typeof(Color))
            {
                var color = (Color)updatedValue;
                red.Text = color.Red.ToString();
                green.Text = color.Green.ToString();
                blue.Text = color.Blue.ToString();
                alpha.Text = color.Alpha.ToString();
            }
            else
            {
                var thickness = (Thickness)updatedValue;
                red.Text = thickness.Left.ToString();
                green.Text = thickness.Top.ToString();
                blue.Text = 0.ToString();
                alpha.Text = 0.ToString();
            }
        }
    }
}