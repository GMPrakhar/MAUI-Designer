

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

using MAUIDesigner.DnDHelper;
using MauiIcons.Core;
using MauiIcons.Fluent;
using System.Diagnostics;

public partial class Designer : ContentPage
{
    private View? MenuDraggerView = null;
    
    private const string defaultXaml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>\r\n<AbsoluteLayout\r\n    Margin=\"20,20,20,20\"\r\n    IsPlatformEnabled=\"True\"\r\n    StyleId=\"designerFrame\"\r\n>\r\n\r\n<Button\r\n    Text=\"Login\"\r\n    Margin=\"114,150,205,195\"\r\n    HeightRequest=\"45\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"91\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<BoxView\r\n    Margin=\"5,-16,329,249\"\r\n    BackgroundColor=\"#17FFFFFF\"\r\n    HeightRequest=\"265\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"324\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Username \"\r\n    Margin=\"26,21,25,20\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Password \"\r\n    Margin=\"26,70,25,69\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Line\r\n    Margin=\"14,378,13,377\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"110,16,305,48\"\r\n    HeightRequest=\"32\"\r\n    IsEnabled=\"True\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"195\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"111,67,311,97\"\r\n    HeightRequest=\"30\"\r\n    IsEnabled=\"True\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"200\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n</AbsoluteLayout>\r\n\r\n</ContentPage>\r\n";

    public Designer()
    {
        InitializeComponent();

        // Add drop gesture recognizer to designerFrame
        var dropGestureRecognizer = new DropGestureRecognizer();
        dropGestureRecognizer.Drop += DragAndDropOperations.OnDrop;
        designerFrame.GestureRecognizers.Add(dropGestureRecognizer);

        ToolBox.contextMenu.UpdateCollectionView();
        ToolBox.MainDesignerView = designerFrame;
        ToolBox.AddElementsForToolbox(Toolbox);

        XAMLHolder.Text = defaultXaml;
        this.LoadViewFromXaml(XAMLHolder, null);
        //PropertiesFrame.IsVisible = false;

        // // Add right-click gesture recognizer to the designer frame
        var rightClickRecognizer = new TapGestureRecognizer();
        rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
        rightClickRecognizer.Buttons = ButtonsMask.Secondary;
        designerFrame.GestureRecognizers.Add(rightClickRecognizer);

        DragAndDropOperations.OnFocusChanged += UpdatePropertyForFocusedView;
    }

    private void UpdatePropertyForFocusedView(object obj)
    {
        Properties.Clear();
        if (obj is ElementDesignerView designerView)
        {
            PropertyHelper.PopulatePropertyView(Properties, designerView.View);
        }
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        // Check if the event location has a child editor element on the location
        var location = e.GetPosition(designerFrame).Value;

        if (ToolBox.contextMenu.IsVisible && !ToolBox.contextMenu.Frame.Contains(location))
        {
            ToolBox.contextMenu.Close();
        }

        DragAndDropOperations.OnFocusChanged(designerFrame);
    }

    private void GenerateXamlForTheView(object sender, EventArgs e)
    {
        var xaml = XAMLGenerator.GetXamlForElement(designerFrame);
        XAMLHolder.Text = xaml;
    }

    private void LoadViewFromXaml(object sender, EventArgs e)
    {
        var xaml = XAMLHolder.Text;
        designerFrame.Children.Clear();

        var newLayout = new AbsoluteLayout();

        try
        {
            var xamlLoaded = Extensions.LoadFromXaml(newLayout, xaml);
            var loadedLayout = newLayout.Children[0] as AbsoluteLayout;
            var newAbsoluteLayout = new AbsoluteLayout();
            LoadLayoutRecursively(newAbsoluteLayout, loadedLayout);

            AddDirectChildrenOfAbsoluteLayout(newAbsoluteLayout);

            designerFrame.Add(ToolBox.contextMenu);
        }
        catch(Exception ex)
        {
            Application.Current.MainPage.DisplayAlert("Error", "Invalid XAML", "OK");
        }
    }

    private void AddDirectChildrenOfAbsoluteLayout(AbsoluteLayout? loadedLayout)
    {

        foreach (View loadedView in loadedLayout.Children)
        {
            designerFrame.Add(loadedView);
        }
    }

    private void LoadLayoutRecursively(Layout newLayout, Layout? loadedLayout)
    {

        foreach (View loadedView in loadedLayout.Children)
        {
            ElementDesignerView elementDesignerView;
            if (loadedView is Layout internalLayout)
            {
                var newLoadedLayout = Activator.CreateInstance(internalLayout.GetType()) as Layout;
                LoadLayoutRecursively(newLoadedLayout, internalLayout);

                elementDesignerView = new ElementDesignerView(newLoadedLayout);
            }
            else
            {
                elementDesignerView = new ElementDesignerView(loadedView);
            }

            newLayout.Add(elementDesignerView);
            ElementOperations.AddDesignerGestureControls(loadedView);
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

    private void DropGestureRecognizer_Drop_1(object sender, DropEventArgs e)
    {
        DragAndDropOperations.OnDrop(sender, e);
    }
}