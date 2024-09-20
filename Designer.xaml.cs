

using MAUIDesigner.HelperViews;
using MAUIDesigner.XamlHelpers;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using Extensions = Microsoft.Maui.Controls.Xaml.Extensions;
using Inputs = Microsoft.UI.Input;
using Xamls = Microsoft.UI.Xaml;
namespace MAUIDesigner;

using MauiIcons.Core;
using MauiIcons.Fluent;
using System.Diagnostics;

public partial class Designer : ContentPage
{
    private bool isDragging = false;
    private bool dragStarted = false;
    private bool isScaling = false;
    private View? focusedView;
    private Rectangle? scalerRect;
    private ISet<Type> nonTappableTypes = new HashSet<Type> { typeof(Editor) };
    private IList<View> nonTappableViews = new ObservableCollection<View>();
    private IDictionary<Guid, View> views = new Dictionary<Guid, View>();
    private SortedDictionary<string, Grid>? PropertiesForFocusedView;
    private View? MenuDraggerView = null;
    private ICollection<string> GuiUpdatableProperties = new [] { "Margin", "HeightRequest", "WidthRequest" };
    private Stack<Action> undoStack = new Stack<Action>();
    private Stack<Action> redoStack = new Stack<Action>();
    private ContextMenu contextMenu = new ContextMenu()
    {
        IsVisible = false
    };
    private const string defaultXaml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>\r\n<AbsoluteLayout\r\n    Margin=\"20,20,20,20\"\r\n    IsPlatformEnabled=\"True\"\r\n    StyleId=\"designerFrame\"\r\n>\r\n\r\n<Button\r\n    Text=\"Login\"\r\n    Margin=\"114,150,205,195\"\r\n    HeightRequest=\"45\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"91\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<BoxView\r\n    Margin=\"5,-16,329,249\"\r\n    BackgroundColor=\"#17FFFFFF\"\r\n    HeightRequest=\"265\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"324\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Username \"\r\n    Margin=\"26,21,25,20\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Password \"\r\n    Margin=\"26,70,25,69\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Line\r\n    Margin=\"14,378,13,377\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"110,16,305,48\"\r\n    HeightRequest=\"32\"\r\n    IsEnabled=\"True\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"195\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"111,67,311,97\"\r\n    HeightRequest=\"30\"\r\n    IsEnabled=\"True\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"200\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n</AbsoluteLayout>\r\n\r\n</ContentPage>\r\n";

    public Designer()
	{
		InitializeComponent();
        //UpdateContextMenuWithRandomProperties();
        
        contextMenu.UpdateCollectionView();

        var allVisualElements = ToolBox.GetAllVisualElementsAlongWithType();

        foreach (var viewType in allVisualElements.Keys)
        {
            var viewsForType = allVisualElements[viewType];

            var label = new Label
            {
                Text = viewType.ToString(),
                FontSize = 15,
                Padding = new Thickness(10, 10, 0 ,0),
                HorizontalOptions = LayoutOptions.Start,
                FontAttributes = FontAttributes.Bold,
            };
            Toolbox.Children.Add(label);

            foreach (var view in viewsForType)
            {
                var tmpGrid = new Grid
                {
                    RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
                    ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
                    HorizontalOptions = LayoutOptions.Start
                };

                var iconImage = new Image
                {
                    Source = new FontImageSource
                    {
                        FontFamily = "FluentIcons", // Ensure this matches the font family name in your project
                        //Glyph = item3, // Use the specific icon name
                        Glyph = view.Item3,
                        //Size = Constants.ToolBoxItemImageSize,
                        Color = Colors.White
                    },
                    WidthRequest = Constants.ToolBoxItemImageWidth,
                    HeightRequest = Constants.ToolBoxItemImageHeight,
                    VerticalOptions = LayoutOptions.Center
                };

                var textLabel = new Label
                {
                    FontSize = Constants.ToolBoxItemLabelSize,
                    TextColor = Application.Current.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
                    Padding = new Thickness(3),
                    BackgroundColor = Color.FromRgba(0, 0, 0, 0),
                    Text = " " + view.Item1,
                    VerticalOptions = LayoutOptions.Center
                };

                var horizontalStack = new HorizontalStackLayout
                {
                    Children = { iconImage, textLabel }
                };

                tmpGrid.Children.Add(horizontalStack);
                Grid.SetColumn(horizontalStack, 0);

                var gestureRecognizer = new TapGestureRecognizer();
                gestureRecognizer.Tapped += CreateElementInDesignerFrame;
                var pointerGestureRecognizer = new PointerGestureRecognizer();
                pointerGestureRecognizer.PointerEntered += RaiseLabel;
                pointerGestureRecognizer.PointerExited += MakeLabelDefault;
                horizontalStack.GestureRecognizers.Add(gestureRecognizer);
                horizontalStack.GestureRecognizers.Add(pointerGestureRecognizer);

                Toolbox.Children.Add(tmpGrid);
            }
        }

        XAMLHolder.Text = defaultXaml;
        this.LoadViewFromXaml(XAMLHolder, null);
        //PropertiesFrame.IsVisible = false;

        // // Add right-click gesture recognizer to the designer frame
        var rightClickRecognizer = new TapGestureRecognizer();
        rightClickRecognizer.Tapped += ShowContextMenu;
        rightClickRecognizer.Buttons = ButtonsMask.Secondary;
        designerFrame.GestureRecognizers.Add(rightClickRecognizer);
    }

        private void RaiseLabel(object? sender, PointerEventArgs e)
    {
        var senderView = sender as HorizontalStackLayout;
        if (senderView != null && senderView.Children.Count > 1)
        {
            var label = senderView.Children[1] as Label;
            if (label != null)
            {
                var animation = new Animation(s => label.FontSize = s, Constants.ToolBoxItemLabelSize, Constants.ToolBoxItemLabelAnimateSize);
                label.Animate("FontSize", animation, 16, 100);
            }
        }
    }

    private void MakeLabelDefault(object? sender, PointerEventArgs e)
    {
        var senderView = sender as HorizontalStackLayout;
        if (senderView != null && senderView.Children.Count > 1)
        {
            var label = senderView.Children[1] as Label;
            if (label != null)
            {
                var animation = new Animation(s => label.FontSize = s, Constants.ToolBoxItemLabelAnimateSize, Constants.ToolBoxItemLabelSize);
                label.Animate("FontSize", animation, 16, 100);
            }
        }
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        // Check if the event location has a child editor element on the location
        var location = e.GetPosition(designerFrame).Value;

        focusedView = nonTappableViews.FirstOrDefault(x => x.Frame.Contains(location));

        if (focusedView != null)
        {
            PropertiesFrame.IsVisible = true;
            AddBorder(focusedView, null);
            PopulatePropertyGridField();
            UpdateActualPropertyView();
        }
        else
        {
            RemoveBorder(focusedView, null);
        }


        if (contextMenu.IsVisible && !contextMenu.Frame.Contains(location))
        {
            contextMenu.Close();
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
            var senderView = sender as HorizontalStackLayout;
            var newElement = ElementCreator.Create((senderView.Children[1] as Label).Text.Trim());
            AddDesignerGestureControls(newElement);
            designerFrame.Add(newElement);
            views.Add(newElement.Id, newElement);

            if (nonTappableTypes.Contains(newElement.GetType()))
            {
                nonTappableViews.Add(newElement);
            }
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

        var rightClickRecognizer = new TapGestureRecognizer();
        rightClickRecognizer.Tapped += ShowContextMenu;
        rightClickRecognizer.Buttons = ButtonsMask.Secondary;

        // Cursor changes to pointer on an Element
        var pointerGestureRecognizer = new PointerGestureRecognizer();
        pointerGestureRecognizer.PointerEntered += (s, e) => ChangeCursorToHand(s);
        pointerGestureRecognizer.PointerExited += (s, e) => ChangeCursorToDefault(s);

        newElement.GestureRecognizers.Add(tapGestureRecognizer);
        newElement.GestureRecognizers.Add(rightClickRecognizer);
        newElement.GestureRecognizers.Add(pointerGestureRecognizer);
    }

    private void ChangeCursorToHand(object sender)
    {
        var view = sender as View;
        if (view != null)
        {
            (view.Handler.PlatformView as Xamls.UIElement).ChangeCursor(Inputs.InputSystemCursor.Create(Inputs.InputSystemCursorShape.Hand));
        }
    }

    private void ChangeCursorToDefault(object sender)
    {
        var view = sender as View;
        if (view != null)
        {
            (view.Handler.PlatformView as Xamls.UIElement).ChangeCursor(Inputs.InputSystemCursor.Create(Inputs.InputSystemCursorShape.Arrow));
        }
    }

    private void ShowContextMenu(object? sender, TappedEventArgs e)
    {
        contextMenu.Show(e, views, designerFrame);
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
        if(senderView is Layout layout)
        {
            senderView = layout.Children.FirstOrDefault(x => x.Frame.Contains(e.GetPosition(senderView).Value)) as View ?? senderView;
        }
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
            var viewParentMargin = (senderView.Parent as View)?.Margin ?? Thickness.Zero;

            if (senderView.Parent == designerFrame) viewParentMargin = Thickness.Zero;
            else
            {
                viewParentMargin +=  new Thickness(senderView.X, senderView.Y);
            }

            gradientBorder2.Margin = new Thickness(senderView.Margin.Left - scaleX/2 + viewParentMargin.Left, senderView.Margin.Top - scaleY/2 + viewParentMargin.Top);

        }catch(Exception)
        {

        }
    }

    private void UndoButton_Clicked(object sender, EventArgs e)
    {
        Undo();
    }

    private void RedoButton_Clicked(object sender, EventArgs e)
    {
        Redo();
    }

    private void Undo()
    {
        if (undoStack.Count > 0)
        {
            var action = undoStack.Pop();
            action.Invoke();
            redoStack.Push(action);
        }
    }

    private void Redo()
    {
        if (redoStack.Count > 0)
        {
            var action = redoStack.Pop();
            action.Invoke();
            undoStack.Push(action);
        }
    }

    private void PointerGestureRecognizer_PointerMoved(object sender, PointerEventArgs e)
    {
        Point location = e.GetPosition(designerFrame).Value;

        if ((!dragStarted && !isScaling) || focusedView == null) return;

        if(dragStarted) isDragging = true;

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

    private void GridPointerMoved(object sender, PointerEventArgs e)
    {

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

        dragStarted = true;
    }

    private void PointerGestureRecognizer_PointerReleased(object sender, PointerEventArgs e)
    {
        if (focusedView != null)
        {
            var location = e.GetPosition(designerFrame).Value;
            if (isDragging)
            {
                var droppedOnLayout = views.Values.Where(x => x is Layout && x != focusedView).FirstOrDefault(x => x.Frame.Contains(location)) ?? designerFrame;

                if (droppedOnLayout != designerFrame)
                {
                    focusedView.Margin = 0;
                }
                
                (focusedView.Parent as Layout).Remove(focusedView);
                (droppedOnLayout as Layout).Add(focusedView);
            }
        }

        dragStarted = false;
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
                VerticalTextAlignment = TextAlignment.Center,
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
                Padding = 8,
                InputTransparent = true,
                CascadeInputTransparent = false,

            };

            grid.VerticalOptions = LayoutOptions.Start;

            grid.Add(label);
            grid.Add(value);

            grid.SetColumn(label, 0);
            grid.SetColumn(value, 1);

            grid.SetRow(label, 0);
            grid.SetRow(value, 0);

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
        Properties.IsVisible = true;
    }

    private void LoadViewFromXaml(object sender, EventArgs e)
    {
        var xaml = XAMLHolder.Text;
        //xaml = this.RemoveContentPageFromXaml(xaml);
        focusedView = null;
        foreach(View view in designerFrame.Where(x => x != gradientBorder2 && x is View).ToList())
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
            LoadLayoutRecursively(loadedLayout);

            AddDirectChildrenOfAbsoluteLayout(loadedLayout);

            designerFrame.Add(contextMenu);

            RemoveBorder(sender, null);
        }
        catch(Exception ex)
        {
            Application.Current.MainPage.DisplayAlert("Error", "Invalid XAML", "OK");
        }
    }

    private void AddDirectChildrenOfAbsoluteLayout(AbsoluteLayout? loadedLayout)
    {

        foreach (View loadedView in loadedLayout.Children.Where(x => x is not Microsoft.Maui.Controls.Layout))
        {
            designerFrame.Add(loadedView);
        }
    }

    private void LoadLayoutRecursively(Layout? loadedLayout)
    {
        foreach (View loadedView in loadedLayout.Children)
        {
            if(loadedView is Layout internalLayout)
            {
                designerFrame.Add(loadedView);
                LoadLayoutRecursively(internalLayout);
            }

            views.Add(loadedView.Id, loadedView);
            AddDesignerGestureControls(loadedView);
        }
    }

    private void DragGestureRecognizer_DragStarting_1(object sender, DragStartingEventArgs e)
    {
        MenuDraggerView = (sender as GestureRecognizer).Parent as View;
    }

    private void DropGestureRecognizer_Drop(object sender, DropEventArgs e)
    {
        var pointerPosition = e.GetPosition(MainGrid).Value;

        if (MenuDraggerView == TabDraggerLeft)
        {
            //(TabDragger.Parent as Layout).WidthRequest = pointerPosition.X;
            MainGrid.ColumnDefinitions.First().Width = pointerPosition.X;
        }
        else if (MenuDraggerView == TabDraggerRight)
        {
            //(TabDragger.Parent as Layout).WidthRequest = pointerPosition.X;
            MainGrid.ColumnDefinitions.Last().Width = MainGrid.Width - pointerPosition.X;
        }
        else if (MenuDraggerView == TabDraggerBottom)
        {
            //(TabDragger.Parent as Layout).WidthRequest = pointerPosition.X;
            MainGrid.RowDefinitions.Last().Height = MainGrid.Height - pointerPosition.Y;
        }

        MenuDraggerView = null;
    }

    private void TabDraggerEntered(object sender, PointerEventArgs e)
    {
        ((sender as View).Handler.PlatformView as Xamls.UIElement).ChangeCursor(Inputs.InputSystemCursor.Create(Inputs.InputSystemCursorShape.SizeAll));
    }

    private void TabDraggerExited(object sender, PointerEventArgs e)
    {
        ((sender as View).Handler.PlatformView as Xamls.UIElement).ChangeCursor(Inputs.InputSystemCursor.Create(Inputs.InputSystemCursorShape.UpArrow));
    }
}