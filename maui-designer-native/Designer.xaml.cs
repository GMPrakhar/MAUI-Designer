using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;
using MAUIDesigner.Resources;
using MAUIDesigner.Services;
using MAUIDesigner.XamlHelpers;

namespace MAUIDesigner;

public partial class Designer : ContentPage
{
    private readonly ICursorService _cursorService;
    private readonly IXamlService _xamlService;
    private readonly ITabSetupService _tabSetupService;
    private readonly IGestureSetupService _gestureSetupService;

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

        InitializeDesigner();
    }

    private void InitializeDesigner()
    {
        _tabSetupService.SetupTabs(
            LeftTabMenuHolder,
            RightTabMenuHolder,
            BottomTabMenuHolder,
            designerFrame,
            () => GenerateXamlForTheView(this, EventArgs.Empty),
            () => LoadViewFromXaml(this, EventArgs.Empty)
        );

        ToolBox.contextMenu = contextMenu;
        ToolBox.contextMenu.UpdateCollectionView();
        ToolBox.MainDesignerView = designerFrame;
        ToolBox.AddElementsForToolbox(_tabSetupService.ToolboxTab.ToolboxLayout);

        _tabSetupService.XamlEditorTab.XamlEditor.Text = DefaultXaml.Content;
        LoadViewFromXaml(_tabSetupService.XamlEditorTab.XamlEditor, EventArgs.Empty);

        _gestureSetupService.SetupDesignerGestures(designerFrame, contextMenu, TapGestureRecognizer_Tapped);
        _gestureSetupService.SetupTabDraggers(TabDraggerLeft, TabDraggerRight, TabDraggerBottom, TabDragger_PanUpdated, _cursorService);

        DragAndDropOperations.OnFocusChanged += UpdatePropertyForFocusedView;
    }

    private void TabDragger_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (sender is not View rect)
        {
            return;
        }

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
                _initialHeight = MainGrid.RowDefinitions[2].Height.Value;

            _cursorService.SetResizeCursor(rect);
        }
        else if (e.StatusType == GestureStatus.Running && MenuDraggerView != null)
        {
            if (MenuDraggerView == TabDraggerLeft)
            {
                var maxWidth = this.Width * Constants.MaximumPanelWidthRatio;
                var newWidth = Math.Max(Constants.MinimumPanelWidth,
                                      Math.Min(maxWidth, _initialWidth + (e.TotalX - _panStartX)));
                MainGrid.ColumnDefinitions[0].Width = new GridLength(newWidth);
            }
            else if (MenuDraggerView == TabDraggerRight)
            {
                var maxWidth = this.Width * Constants.MaximumPanelWidthRatio;
                var newWidth = Math.Max(Constants.MinimumPanelWidth,
                                      Math.Min(maxWidth, _initialWidth - (e.TotalX - _panStartX)));
                MainGrid.ColumnDefinitions[2].Width = new GridLength(newWidth);
            }
            else if (MenuDraggerView == TabDraggerBottom)
            {
                var maxHeight = this.Height * Constants.MaximumPanelHeightRatio;
                var newHeight = Math.Max(Constants.MinimumPanelHeight,
                                       Math.Min(maxHeight, _initialHeight - (e.TotalY - _panStartY)));
                MainGrid.RowDefinitions[2].Height = new GridLength(newHeight);
            }
        }
        else if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Canceled)
        {
            MenuDraggerView = null;
            _cursorService.SetDefaultCursor(rect);
        }
    }

    private void UpdatePropertyForFocusedView(object? obj)
    {
        _tabSetupService.PropertiesTab.PropertiesLayout.Clear();
        if (obj is ElementDesignerView designerView)
        {
            PropertyHelper.PopulatePropertyView(_tabSetupService.PropertiesTab.PropertiesLayout, designerView.View);
        }
    }

    private void TapGestureRecognizer_Tapped(object? sender, TappedEventArgs e)
    {
        var location = e.GetPosition(designerFrame);
        if (location is not Point point)
        {
            return;
        }

        if (ToolBox.contextMenu.IsVisible && !ToolBox.contextMenu.Frame.Contains(point))
        {
            ToolBox.contextMenu.Close();
        }
        DragAndDropOperations.OnFocusChanged?.Invoke(designerFrame);
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
}
