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
    private ToolboxTab _toolboxTab;
    private PropertiesTab _propertiesTab;
    private XamlEditorTab _xamlEditorTab;
    private HierarchyTab _hierarchyTab;



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
        ToolBox.contextMenu = contextMenu;
        ToolBox.contextMenu.UpdateCollectionView();
        ToolBox.MainDesignerView = designerFrame;
        ToolBox.AddElementsForToolbox(_toolboxTab.ToolboxLayout);

        // Setup XAML editor
        _xamlEditorTab.XamlEditor.Text = DefaultXaml.Content;
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
        AttachPointerGestureRecognizer(TabDraggerLeft, CursorType.SizeWE);
        AttachPointerGestureRecognizer(TabDraggerRight, CursorType.SizeWE);
        AttachPointerGestureRecognizer(TabDraggerBottom, CursorType.SizeNS);
    }

    private void AttachPanGestureRecognizer(Rectangle tabDragger)
    {
        var panGestureRecognizer = new PanGestureRecognizer();
        panGestureRecognizer.PanUpdated += TabDragger_PanUpdated;
        tabDragger.GestureRecognizers.Add(panGestureRecognizer);
    }

    private void AttachPointerGestureRecognizer(Rectangle tabDragger, CursorType cursorType)
    {
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += (s, e) => _cursorService.SetCursor(tabDragger, cursorType);
        pointerGesture.PointerExited += (s, e) => _cursorService.SetCursor(tabDragger, CursorType.Arrow);
        tabDragger.GestureRecognizers.Add(pointerGesture);
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
        _xamlService.LoadViewFromXaml(xaml, designerFrame, _hierarchyTab);
    }



    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
    }
}