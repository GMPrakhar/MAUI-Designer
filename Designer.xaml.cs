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
using Microsoft.Maui.Controls;

public partial class Designer : ContentPage
{
    private ToolboxTab _toolboxTab;
    private PropertiesTab _propertiesTab;
    private XamlEditorTab _xamlEditorTab;
    private HierarchyTab _hierarchyTab;

    private const string defaultXaml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>\r\n<AbsoluteLayout\r\n    Margin=\"20,20,20,20\"\r\n    IsPlatformEnabled=\"True\"\r\n    StyleId=\"designerFrame\"\r\n>\r\n\r\n<Button\r\n    Text=\"Login\"\r\n    Margin=\"114,150,205,195\"\r\n    HeightRequest=\"45\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"91\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<BoxView\r\n    Margin=\"5,-16,329,249\"\r\n    BackgroundColor=\"#17FFFFFF\"\r\n    HeightRequest=\"265\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"324\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Username \"\r\n    Margin=\"26,21,25,20\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Label\r\n    Text=\"Password \"\r\n    Margin=\"26,70,25,69\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Line\r\n    Margin=\"14,378,13,377\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"110,16,305,48\"\r\n    HeightRequest=\"32\"\r\n    IsEnabled=\"True\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"195\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n<Editor\r\n    Text=\"Type here\"\r\n    TextColor=\"#FF404040\"\r\n    Margin=\"111,67,311,97\"\r\n    HeightRequest=\"30\"\r\n    IsEnabled=\"True\"\r\n    MinimumHeightRequest=\"20\"\r\n    MinimumWidthRequest=\"20\"\r\n    WidthRequest=\"200\"\r\n    IsPlatformEnabled=\"True\"\r\n/>\r\n\r\n</AbsoluteLayout>\r\n\r\n</ContentPage>\r\n";

    private View? MenuDraggerView = null;
    private double _initialWidth = 0;
    private double _initialHeight = 0;
    private double _panStartX = 0;
    private double _panStartY = 0;

    public Designer()
    {
        InitializeComponent();

        // Instantiate tabs
        _toolboxTab = new ToolboxTab();
        _propertiesTab = new PropertiesTab();
        _hierarchyTab = new HierarchyTab();
        _xamlEditorTab = new XamlEditorTab(
            () => GenerateXamlForTheView(this, null),
            () => LoadViewFromXaml(this, null)
        );

        // Add tabs to holders
        BottomTabMenuHolder.AddTab(_xamlEditorTab);
        LeftTabMenuHolder.AddTab(_toolboxTab);
        RightTabMenuHolder.AddTab(_propertiesTab);
        RightTabMenuHolder.AddTab(_hierarchyTab);

        // Setup toolbox
        ToolBox.contextMenu.UpdateCollectionView();
        ToolBox.MainDesignerView = designerFrame;
        ToolBox.AddElementsForToolbox(_toolboxTab.ToolboxLayout);

        // Setup XAML editor
        _xamlEditorTab.XamlEditor.Text = defaultXaml;
        LoadViewFromXaml(_xamlEditorTab.XamlEditor, null);

        // Setup context menu
        var rightClickRecognizer = new TapGestureRecognizer();
        rightClickRecognizer.Tapped += ToolBox.ShowContextMenu;
        rightClickRecognizer.Buttons = ButtonsMask.Secondary;
        designerFrame.GestureRecognizers.Add(rightClickRecognizer);

        // Setup drag and drop
        var dropGestureRecognizer = new DropGestureRecognizer();
        dropGestureRecognizer.Drop += DragAndDropOperations.OnDrop;
        designerFrame.GestureRecognizers.Add(dropGestureRecognizer);

        DragAndDropOperations.OnFocusChanged += UpdatePropertyForFocusedView;
        DragAndDropOperations.BaseLayout = designerFrame;

        // Setup hierarchy tab
        _hierarchyTab.SetDesignerFrame(designerFrame);
        designerFrame.ChildAdded += (s, e) => _hierarchyTab.UpdateHierarchy();
        designerFrame.ChildRemoved += (s, e) => _hierarchyTab.UpdateHierarchy();

        // Attach PanGestureRecognizer for TabDragger rectangles
        AttachPanGestureRecognizer(TabDraggerLeft);
        AttachPanGestureRecognizer(TabDraggerRight);
        AttachPanGestureRecognizer(TabDraggerBottom);

        // Attach pointer recognizers for draggers
        AttachPointerGestureRecognizer(TabDraggerLeft, "SizeWE");
        AttachPointerGestureRecognizer(TabDraggerRight, "SizeWE");
        AttachPointerGestureRecognizer(TabDraggerBottom, "SizeNS");
    }

    private void AttachPanGestureRecognizer(Rectangle tabDragger)
    {
        var panGestureRecognizer = new PanGestureRecognizer();
        panGestureRecognizer.PanUpdated += TabDragger_PanUpdated;
        tabDragger.GestureRecognizers.Add(panGestureRecognizer);
    }

    private void AttachPointerGestureRecognizer(Rectangle tabDragger, string cursorType)
    {
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += (s, e) => SetCursor(tabDragger, cursorType);
        pointerGesture.PointerExited += (s, e) => SetCursor(tabDragger, "Arrow");
        tabDragger.GestureRecognizers.Add(pointerGesture);
    }

    private void SetCursor(View view, string cursorType)
    {
#if WINDOWS
        if (view.Handler?.PlatformView is Xamls.UIElement element)
        {
            var shape = cursorType switch
            {
                "SizeWE" => Inputs.InputSystemCursorShape.SizeWestEast,
                "SizeNS" => Inputs.InputSystemCursorShape.SizeNorthSouth,
                _ => Inputs.InputSystemCursorShape.Arrow
            };
            element.ChangeCursor(Inputs.InputSystemCursor.Create(shape));
        }
#endif
    }

    private void TabDragger_PanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (sender is Rectangle rect)
        {
            if (e.StatusType == GestureStatus.Started)
            {
                MenuDraggerView = rect;
                _panStartX = e.TotalX;
                _panStartY = e.TotalY;
                if (rect == TabDraggerLeft)
                    _initialWidth = MainGrid.ColumnDefinitions[0].Width.Value;
                else if (rect == TabDraggerRight)
                    _initialWidth = MainGrid.ColumnDefinitions[2].Width.Value;
                else if (rect == TabDraggerBottom)
                    _initialHeight = MainGrid.RowDefinitions[1].Height.Value;
            }
            else if (e.StatusType == GestureStatus.Running && MenuDraggerView != null)
            {
                if (MenuDraggerView == TabDraggerLeft)
                {
                    var newWidth = Math.Max(50, _initialWidth + (e.TotalX - _panStartX));
                    MainGrid.ColumnDefinitions[0].Width = new GridLength(newWidth);
                }
                else if (MenuDraggerView == TabDraggerRight)
                {
                    var newWidth = Math.Max(50, _initialWidth - (e.TotalX - _panStartX));
                    MainGrid.ColumnDefinitions[2].Width = new GridLength(newWidth);
                }
                else if (MenuDraggerView == TabDraggerBottom)
                {
                    var newHeight = Math.Max(50, _initialHeight - (e.TotalY - _panStartY));
                    MainGrid.RowDefinitions[1].Height = new GridLength(newHeight);
                }
            }
            else if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Canceled)
            {
                MenuDraggerView = null;
            }
        }
    }

    private void UpdatePropertyForFocusedView(object obj)
    {
        _propertiesTab.PropertiesLayout.Clear();
        if (obj is ElementDesignerView designerView)
        {
            PropertyHelper.PopulatePropertyView(_propertiesTab.PropertiesLayout, designerView.View);
        }
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
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
        _xamlEditorTab.XamlEditor.Text = xaml;
    }

    private void LoadViewFromXaml(object sender, EventArgs e)
    {
        var xaml = _xamlEditorTab.XamlEditor.Text;
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
            _hierarchyTab.UpdateHierarchy();
        }
        catch
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
                CopyProperties(internalLayout, newLoadedLayout);
                LoadLayoutRecursively(newLoadedLayout, internalLayout);
                elementDesignerView = new ElementDesignerView(newLoadedLayout);
            }
            else
            {
                elementDesignerView = new ElementDesignerView(loadedView);
            }
            newLayout.Add(elementDesignerView);
            ElementOperations.AddDesignerGestureControls(elementDesignerView);
        }
        DragAndDropOperations.OnFocusChanged.Invoke(null);
    }

    private void CopyProperties(Layout internalLayout, Layout? newLoadedLayout)
    {
        if (newLoadedLayout == null)
            return;
        var properties = internalLayout.GetType().GetProperties();
        foreach (var property in properties)
        {
            if (property.CanRead && property.CanWrite && property.Name != "Item")
            {
                var value = property.GetValue(internalLayout);
                property.SetValue(newLoadedLayout, value);
            }
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
    }
}