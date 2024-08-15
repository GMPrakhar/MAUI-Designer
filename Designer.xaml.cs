

using MAUIDesigner.XamlHelpers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Extensions = Microsoft.Maui.Controls.Xaml.Extensions;
namespace MAUIDesigner;

public partial class Designer : ContentPage
{
    private bool isDragging = false;
    private bool isScaling = false;
    private View? focusedView;
    private Rectangle? scalerRect;
    private ISet<Type> nonTappableTypes = new HashSet<Type> { typeof(Editor) };
    private IList<View> nonTappableViews = new List<View>();
    private IDictionary<Guid, View> views = new Dictionary<Guid, View>();
    private SortedDictionary<string, Grid>? PropertiesForFocusedView;
    private ICollection<string> GuiUpdatableProperties = new [] { "Margin", "HeightRequest", "WidthRequest" };
    private const string defaultXaml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>\r\n<AbsoluteLayout\r\n    Margin=\"20,20,20,20\"\r\n    IsPlatformEnabled=\"True\"\r\n    StyleId=\"designerFrame\"\r\n>\r\n\r\n<Button\r\n    Text=\"Login\"\r\n    Margin=\"114,150,205,195\"\r\n    HeightRequest=\"45\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"91\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<BoxView\r\n    Margin=\"5,-16,329,249\"\r\n    BackgroundColor=\"#17FFFFFF\"\r\n    HeightRequest=\"265\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"324\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Username \"\r\n    Margin=\"26,21,25,20\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Password \"\r\n    Margin=\"26,70,25,69\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Line\r\n    Margin=\"14,378,13,377\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"110,16,305,48\"\r\n    HeightRequest=\"32\"\r\n    IsEnabled=\"False\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"195\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"111,67,311,97\"\r\n    HeightRequest=\"30\"\r\n    IsEnabled=\"False\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"200\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n</AbsoluteLayout>\r\n\r\n</ContentPage>\r\n";

    public Designer()
	{
		InitializeComponent();
        var allVisualElements = ToolBox.GetAllVisualElementsAlongWithType();

        foreach (var viewType in allVisualElements.Keys)
        {
            var viewsForType = allVisualElements[viewType];

            var label = new Label
            {
                Text = viewType.ToString(),
                FontSize = 10,
                Margin = new Thickness(10),
                HorizontalOptions = LayoutOptions.Start,
                FontAttributes = FontAttributes.Bold,
            };
            Toolbox.Children.Add(label);

            foreach (var view in viewsForType)
            {
                var labelView = new Button
                {
                    Text = view.Item1,
                    FontSize = 10,
                    TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
                    BackgroundColor = Color.FromRgba(0,0,0,0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    HorizontalOptions = LayoutOptions.CenterAndExpand,
                };

                var gestureRecognizer = new TapGestureRecognizer();
                gestureRecognizer.Tapped += CreateElementInDesignerFrame;
                var pointerGestureRecognizer = new PointerGestureRecognizer();
                pointerGestureRecognizer.PointerEntered += RaiseLabel;
                pointerGestureRecognizer.PointerExited += MakeLabelDefault;
                labelView.GestureRecognizers.Add(gestureRecognizer);
                labelView.GestureRecognizers.Add(pointerGestureRecognizer);

                Toolbox.Children.Add(labelView);
            }
        }

        XAMLHolder.Text = defaultXaml;
        this.LoadViewFromXaml(XAMLHolder, null);
    }

    private void RaiseLabel(object? sender, PointerEventArgs e)
    {
        var senderView = sender as Button;
        var animation = new Animation(s => senderView.FontSize = s, 10, 15);
        senderView.Animate("FontSize", animation, 16, 100);
    }

    private void MakeLabelDefault(object? sender, PointerEventArgs e)
    {
        var senderView = sender as Button;
        // Animate Font size for senderView
        var animation = new Animation(s => senderView.FontSize = s, 15, 10);
        senderView.Animate("FontSize", animation, 16, 100);
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
            var newElement = ElementCreator.Create((sender as Button).Text);
            AddDesignerGestureControls(newElement);
            designerFrame.Add(newElement);
            views.Add(newElement.Id, newElement);

            if (nonTappableTypes.Contains(newElement.GetType()))
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

    private void AddDesignerGestureControls(View newElement)
    {
        newElement.PropertyChanged += ElementPropertyChanged;
        var tapGestureRecognizer = new TapGestureRecognizer();
        tapGestureRecognizer.Tapped += EnableElementForOperations;
        tapGestureRecognizer.Buttons = ButtonsMask.Primary | ButtonsMask.Secondary;
        newElement.GestureRecognizers.Add(tapGestureRecognizer);
    }

    private void ElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(sender == focusedView)
        {
            RemoveBorder(sender, null);
            AddBorder(sender, null);
            //UpdatePropertyForFocusedView(e.PropertyName, focusedView.GetType().GetProperty(e.PropertyName)?.GetValue(focusedView));
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

            var scaleX = 15 + (senderView.Width / designerFrame.Width) * 15;
            var scaleY = 15 + (senderView.Height / designerFrame.Height) * 15;

            gradientBorder2.HeightRequest = senderView.Height + scaleX;
            gradientBorder2.WidthRequest = senderView.Width + scaleY;
            gradientBorder2.Opacity = 1;
            gradientBorder2.Margin = new Thickness(senderView.Margin.Left - scaleX/2, senderView.Margin.Top - scaleY/2);
        }catch(Exception)
        {

        }
    }

    private void PointerGestureRecognizer_PointerMoved(object sender, PointerEventArgs e)
    {
        Point location = e.GetPosition(designerFrame).Value;

        if ((!isDragging && !isScaling) || focusedView == null) return;

        gradientBorder2.Opacity = 1;
        // Update margin property for the focusedView using Update function
        location.X = (int)location.X;
        location.Y = (int)location.Y;
        
        if (isScaling)
        {
            Thickness scalingFactor;
            if (scalerRect == topLeftRect)
            {
                scalingFactor = new Thickness(focusedView.Margin.Left - location.X, focusedView.Margin.Top - location.Y);
            }
            else if (scalerRect == topRightRect)
            {
                scalingFactor = new Thickness(location.X - focusedView.Margin.Right, focusedView.Margin.Top - location.Y);

                // Set location X to be same as focused view's margin so it doesn't get updated.
                location.X = focusedView.Margin.Left;
            }
            else if (scalerRect == bottomLeftRect)
            {
                scalingFactor = new Thickness(focusedView.Margin.Left - location.X, location.Y - focusedView.Margin.Bottom);
                location.Y = focusedView.Margin.Top;
            }
            else
            {
                scalingFactor = new Thickness(location.X - focusedView.Margin.Right, location.Y - focusedView.Margin.Bottom);
                location.X = focusedView.Margin.Left;
                location.Y = focusedView.Margin.Top;
            }

            UpdatePropertyForFocusedView("WidthRequest", Math.Max(focusedView.WidthRequest + scalingFactor.Left, 20));
            UpdatePropertyForFocusedView("HeightRequest", Math.Max(focusedView.HeightRequest + scalingFactor.Top, 20));
        }
        
        UpdatePropertyForFocusedView("Margin", new Thickness(location.X, location.Y, location.X + focusedView.WidthRequest, location.Y + focusedView.HeightRequest));
    }

    private async void PointerGestureRecognizer_PointerPressed(object sender, PointerEventArgs e)
    {
        var location = e.GetPosition(gradientBorder2).Value;


        if (topLeftRect.Frame.Contains(location))
        {
            scalerRect = topLeftRect;
            isScaling = true;
            return;
        }
        else if (topRightRect.Frame.Contains(location))
        {
            scalerRect = topRightRect;
            isScaling = true;
            return;
        }
        else if (bottomLeftRect.Frame.Contains(location))
        {
            scalerRect = bottomLeftRect;
            isScaling = true;
            return;
        }
        else if (bottomRightRect.Frame.Contains(location))
        {
            scalerRect = bottomRightRect;
            isScaling = true;
            return;
        }

        isDragging = true;
    }

    private void PointerGestureRecognizer_PointerReleased(object sender, PointerEventArgs e)
    {
        isDragging = false;
        isScaling = false;
        scalerRect = null;
    }

    private void DragGestureRecognizer_DragStarting(object? sender, DragStartingEventArgs e)
    {
    }

    private void DragGestureRecognizer_DropCompleted(object? sender, DropCompletedEventArgs e)
    {
        scalerRect = null;
    }

    private void GenerateXamlForTheView(object sender, EventArgs e)
    {
        var xaml = XAMLGenerator.GetXamlForElement(designerFrame);
        XAMLHolder.Text = xaml;
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
        if (focusedView == null || PropertiesForFocusedView == null || !PropertiesForFocusedView.ContainsKey(propertyName) || !this.GuiUpdatableProperties.Contains(propertyName)) return;
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
        else if (value is Grid thicknessgrid)
        {
            var left = thicknessgrid.Children[0] as Entry;
            var top = thicknessgrid.Children[1] as Entry;
            var right = thicknessgrid.Children[2] as Entry;
            var bottom = thicknessgrid.Children[3] as Entry;

            var thickness = (Thickness)updatedValue;
            left.Text = thickness.Left.ToString();
            top.Text = thickness.Top.ToString();
            right.Text = thickness.Right.ToString();
            bottom.Text = thickness.Bottom.ToString();
        }
    }

    private void LoadViewFromXaml(object sender, EventArgs e)
    {
        var xaml = XAMLHolder.Text;
        //xaml = this.RemoveContentPageFromXaml(xaml);
        focusedView = null;
        foreach(View view in designerFrame.Children.Where(x => x != gradientBorder2 && x is View).ToList())
        {
            designerFrame.Remove(view);
            views.Remove(view.Id);
            nonTappableViews.Remove(view);
        }

        var newLayout = new AbsoluteLayout();

        try
        {
            var xamlLoaded = Extensions.LoadFromXaml(newLayout, xaml);
            var loadedLayout = newLayout.Children[0] as AbsoluteLayout;

            foreach (View loadedView in loadedLayout.Children)
            {
                designerFrame.Add(loadedView);
                views.Add(loadedView.Id, loadedView);
                AddDesignerGestureControls(loadedView);
            }

            RemoveBorder(sender, null);
        }
        catch
        {
            Application.Current.MainPage.DisplayAlert("Error", "Invalid XAML", "OK");
        }
    }
}