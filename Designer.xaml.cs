using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;
using MAUIDesigner.Resources;
using MAUIDesigner.Services;
using MAUIDesigner.XamlHelpers;
using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;
using Extensions = Microsoft.Maui.Controls.Xaml.Extensions;

namespace MAUIDesigner;

public partial class Designer : ContentPage
{
    private readonly ICursorService _cursorService;
    private readonly IXamlService _xamlService;
    private readonly ITabSetupService _tabSetupService;
    private readonly IGestureSetupService _gestureSetupService;
    private readonly Core.Elements.IElementOperations _elementOperations;
    private readonly Core.Properties.IPropertyService _propertyService;



    private View? MenuDraggerView = null;
    private double _initialWidth = 0;
    private double _initialHeight = 0;
    private double _panStartX = 0;
    private double _panStartY = 0;

    public Designer()
    {
        InitializeComponent();
        _cursorService = new CursorService();
        _xamlService = new XamlService();
        _tabSetupService = new TabSetupService();
        _gestureSetupService = new GestureSetupService();
        _elementOperations = new Core.Elements.ElementService(_cursorService);
        _propertyService = new Core.Properties.PropertyService();

        // Set dependencies
        _xamlService.SetElementOperations(_elementOperations);

        InitializeDesigner();
    }

    private void InitializeDesigner()
    {
        // Setup tabs
        _tabSetupService.SetupTabs(
            LeftTabMenuHolder,
            RightTabMenuHolder,
            BottomTabMenuHolder,
            designerFrame,
            () => GenerateXamlForTheView(this, null),
            () => LoadViewFromXaml(this, null)
        );

        // Setup toolbox
        ToolBox.contextMenu = contextMenu;
        ToolBox.contextMenu.UpdateCollectionView();
        ToolBox.MainDesignerView = designerFrame;
        ToolBox.ElementOperations = _elementOperations;
        HelperViews.ContextMenuActions.ElementOperations = _elementOperations;
        ToolBox.AddElementsForToolbox(_tabSetupService.ToolboxTab.ToolboxLayout);

        // Setup XAML editor
        _tabSetupService.XamlEditorTab.XamlEditor.Text = DefaultXaml.Content;
        LoadViewFromXaml(_tabSetupService.XamlEditorTab.XamlEditor, null);

        // Setup gestures
        _gestureSetupService.SetupDesignerGestures(designerFrame, contextMenu, TapGestureRecognizer_Tapped);
        _gestureSetupService.SetupTabDraggers(TabDraggerLeft, TabDraggerRight, TabDraggerBottom, TabDragger_PanUpdated, _cursorService);

        DragAndDropOperations.OnFocusChanged += UpdatePropertyForFocusedView;
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
                    var newWidth = Math.Max(Constants.MinimumPanelWidth, _initialWidth + (e.TotalX - _panStartX));
                    MainGrid.ColumnDefinitions[0].Width = new GridLength(newWidth);
                }
                else if (MenuDraggerView == TabDraggerRight)
                {
                    var newWidth = Math.Max(Constants.MinimumPanelWidth, _initialWidth - (e.TotalX - _panStartX));
                    MainGrid.ColumnDefinitions[2].Width = new GridLength(newWidth);
                }
                else if (MenuDraggerView == TabDraggerBottom)
                {
                    var newHeight = Math.Max(Constants.MinimumPanelHeight, _initialHeight - (e.TotalY - _panStartY));
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
        _tabSetupService.PropertiesTab.PropertiesLayout.Clear();
        if (obj is ElementDesignerView designerView)
        {
            _propertyService.PopulatePropertyView(_tabSetupService.PropertiesTab.PropertiesLayout, designerView.View);
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
        _tabSetupService.XamlEditorTab.XamlEditor.Text = xaml;
    }

    private void LoadViewFromXaml(object sender, EventArgs e)
    {
        var xaml = _tabSetupService.XamlEditorTab.XamlEditor.Text;
        _xamlService.LoadViewFromXaml(xaml, designerFrame, _tabSetupService.HierarchyTab);
    }

    private async void RunInteractiveButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            // Generate XAML from current designer content
            var xaml = XAMLGenerator.GetXamlForElement(designerFrame);
            
            // Navigate to the interactive preview page with the generated XAML
            var navigationParameter = new Dictionary<string, object>
            {
                { "xamlContent", xaml }
            };
            
            await Shell.Current.GoToAsync("interactive-preview", navigationParameter);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating to interactive preview: {ex.Message}");
            await Application.Current.MainPage.DisplayAlert("Error", "Could not open interactive preview: " + ex.Message, "OK");
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
    }
}