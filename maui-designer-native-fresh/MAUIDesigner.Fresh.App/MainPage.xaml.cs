using System.Collections.ObjectModel;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Controls;
using MAUIDesigner.Fresh.App.PropertyEditing;
using MAUIDesigner.Fresh.App.Rendering;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.App.Xaml;
using MAUIDesigner.Fresh.App.Viewport;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Xaml;

namespace MAUIDesigner.Fresh.App;

public partial class MainPage : ContentPage
{
    private readonly IControlCatalog _catalog;
    private readonly DesignerWorkspace _workspace;
    private readonly ControlMaterializer _materializer;
    private readonly PropertyEditorRegistry _propertyEditors;
    private readonly AssemblyExtensionLoader _extensionLoader;
    private readonly XamlWorkspace _xamlWorkspace;
    private readonly DesignerViewportState _viewport;
    private readonly CanvasGridDrawable _gridDrawable;
    private readonly CanvasRulerDrawable _rulerDrawable;
    private readonly ObservableCollection<ControlDescriptor> _toolboxItems = [];
    private readonly ObservableCollection<HierarchyItem> _hierarchyItems = [];
    private bool _updatingXaml;
    private bool _xamlDirty;
    private bool _viewportInitialized;

    public MainPage(
        IControlCatalog catalog,
        DesignerWorkspace workspace,
        ControlMaterializer materializer,
        PropertyEditorRegistry propertyEditors,
        AssemblyExtensionLoader extensionLoader,
        XamlWorkspace xamlWorkspace,
        DesignerViewportState viewport)
    {
        InitializeComponent();
        _catalog = catalog;
        _workspace = workspace;
        _materializer = materializer;
        _propertyEditors = propertyEditors;
        _extensionLoader = extensionLoader;
        _xamlWorkspace = xamlWorkspace;
        _viewport = viewport;
        _gridDrawable = new CanvasGridDrawable(viewport);
        _rulerDrawable = new CanvasRulerDrawable(viewport);
        CanvasGridOverlay.Drawable = _gridDrawable;
        CanvasRulerOverlay.Drawable = _rulerDrawable;
        DevicePicker.ItemDisplayBinding = new Binding(nameof(DevicePreset.Name));
        DevicePicker.ItemsSource = _viewport.Devices.ToList();
        DevicePicker.SelectedItem = _viewport.SelectedDevice;
        GridSizeStepper.Value = _viewport.GridSize;
        BindableLayout.SetItemsSource(ToolboxItemsHost, _toolboxItems);
        HierarchyList.ItemsSource = _hierarchyItems;
        _workspace.Session.Changed += OnDocumentChanged;
        _workspace.SelectionChanged += OnSelectionChanged;
        _workspace.InteractionChanged += OnInteractionChanged;
        _catalog.Changed += OnCatalogChanged;
        ApplyToolboxFilter(string.Empty);
        ShowToolbox(show: true);
        RebuildDesigner();
        RefreshXaml();
        UpdateViewportVisuals();
    }

    private void OnDeviceChanged(object? sender, EventArgs e)
    {
        if (DevicePicker.SelectedItem is not DevicePreset device)
        {
            return;
        }

        _viewport.SelectDevice(device);
        _viewport.Fit(CanvasViewport.Width, CanvasViewport.Height);
        UpdateViewportVisuals();
    }

    private void OnZoomOutClicked(object? sender, EventArgs e) =>
        ZoomAtCenter(_viewport.Zoom - 0.1);

    private void OnZoomInClicked(object? sender, EventArgs e) =>
        ZoomAtCenter(_viewport.Zoom + 0.1);

    private void OnZoomFitClicked(object? sender, EventArgs e)
    {
        _viewport.Fit(CanvasViewport.Width, CanvasViewport.Height);
        UpdateViewportVisuals();
    }

    private void OnZoomResetClicked(object? sender, EventArgs e)
    {
        _viewport.Reset(CanvasViewport.Width, CanvasViewport.Height);
        UpdateViewportVisuals();
    }

    private void OnThemeClicked(object? sender, EventArgs e)
    {
        _viewport.ToggleTheme();
        RebuildDesigner();
        UpdateViewportVisuals();
    }

    private void OnGridClicked(object? sender, EventArgs e)
    {
        _viewport.ToggleGrid();
        UpdateViewportVisuals();
    }

    private void OnSnapClicked(object? sender, EventArgs e)
    {
        _viewport.ToggleSnap();
        UpdateViewportVisuals();
    }

    private void OnGridSizeChanged(object? sender, ValueChangedEventArgs e)
    {
        _viewport.SetGridSize((int)e.NewValue);
        UpdateViewportVisuals();
    }

    private void OnRulersClicked(object? sender, EventArgs e)
    {
        _viewport.ToggleRulers();
        UpdateViewportVisuals();
    }

    private void OnCanvasPanRequested(object? sender, CanvasPanEventArgs e)
    {
        _viewport.PanBy(e.DeltaX, e.DeltaY);
        UpdateViewportVisuals();
    }

    private void OnCanvasZoomRequested(object? sender, CanvasZoomEventArgs e)
    {
        double factor = e.WheelDelta > 0 ? 1.1 : 0.9;
        _viewport.ZoomAt(_viewport.Zoom * factor, e.X, e.Y);
        UpdateViewportVisuals();
    }

    private void OnCanvasViewportSizeChanged(object? sender, EventArgs e)
    {
        if (!_viewportInitialized && CanvasViewport.Width > 0 && CanvasViewport.Height > 0)
        {
            _viewportInitialized = true;
            _viewport.Fit(CanvasViewport.Width, CanvasViewport.Height);
            UpdateViewportVisuals();
        }
    }

    private void ZoomAtCenter(double zoom)
    {
        _viewport.ZoomAt(
            zoom,
            CanvasViewport.Width / 2,
            CanvasViewport.Height / 2);
        UpdateViewportVisuals();
    }

    private void UpdateViewportVisuals()
    {
        CanvasTransformHost.WidthRequest = _viewport.DesignWidth;
        CanvasTransformHost.HeightRequest = _viewport.DesignHeight;
        CanvasTransformHost.Scale = _viewport.Zoom;
        CanvasTransformHost.TranslationX = _viewport.PanX;
        CanvasTransformHost.TranslationY = _viewport.PanY;
        CanvasFrame.WidthRequest = _viewport.DesignWidth;
        CanvasFrame.HeightRequest = _viewport.DesignHeight;
        CanvasFrame.BackgroundColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#111827")
            : Colors.White;
        CanvasViewport.BackgroundColor = _viewport.IsDarkPreview
            ? Color.FromArgb("#080D17")
            : Color.FromArgb("#0B0D12");
        ZoomLabel.Text = $"{Math.Round(_viewport.Zoom * 100)}%";
        ThemeButton.Text = _viewport.IsDarkPreview ? "Light" : "Dark";
        GridButton.BackgroundColor = _viewport.ShowGrid
            ? Color.FromArgb("#5946A3")
            : Color.FromArgb("#202431");
        SnapButton.BackgroundColor = _viewport.SnapToGrid
            ? Color.FromArgb("#5946A3")
            : Color.FromArgb("#202431");
        RulersButton.BackgroundColor = _viewport.ShowRulers
            ? Color.FromArgb("#5946A3")
            : Color.FromArgb("#202431");
        GridSizeLabel.Text = _viewport.GridSize.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        CanvasGridOverlay.Invalidate();
        CanvasRulerOverlay.Invalidate();
    }

    private void OnToolboxSearchChanged(object? sender, TextChangedEventArgs e) =>
        ApplyToolboxFilter(e.NewTextValue ?? string.Empty);

    private void OnPropertySearchChanged(object? sender, TextChangedEventArgs e) =>
        RebuildPropertyPanel();

    private void OnToolboxDragUpdated(object? sender, ToolboxDragEventArgs e)
    {
        if (sender is not ToolboxItemView
            {
                Descriptor: ControlDescriptor descriptor
            } source)
        {
            return;
        }

        if (e.StatusType == GestureStatus.Started)
        {
            SelectionLabel.Text = $"Dragging {descriptor.DisplayName}";
            _materializer.BeginManualDrag(source, movingId: null);
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            ElementId? targetId = _materializer.UpdateManualDrag(e.TotalX, e.TotalY);
            SelectionLabel.Text = targetId is null
                ? $"No valid drop target for {descriptor.DisplayName}"
                : $"Drop {descriptor.DisplayName} into {targetId.Value.Value}";
        }
        else if (e.StatusType == GestureStatus.Completed)
        {
            _materializer.CompleteManualToolboxDrag(descriptor);
        }
        else if (e.StatusType == GestureStatus.Canceled)
        {
            _materializer.CancelManualDrag();
        }
    }

    private void OnToolboxItemTapped(object? sender, EventArgs e)
    {
        if (sender is ToolboxItemView { Descriptor: ControlDescriptor descriptor })
        {
            _workspace.Add(descriptor);
        }
    }

    private void OnToolboxTabClicked(object? sender, EventArgs e) => ShowToolbox(show: true);

    private void OnHierarchyTabClicked(object? sender, EventArgs e) => ShowToolbox(show: false);

    private void OnHierarchySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is HierarchyItem item)
        {
            _workspace.Select(item.ElementId);
        }

        HierarchyList.SelectedItem = null;
    }

    private void OnUndoClicked(object? sender, EventArgs e) => _workspace.Session.Undo();

    private void OnRedoClicked(object? sender, EventArgs e) => _workspace.Session.Redo();

    private void OnDeleteClicked(object? sender, EventArgs e) => _workspace.DeleteSelection();

    private void OnToggleXamlClicked(object? sender, EventArgs e)
    {
        bool opening = !XamlPanel.IsVisible;
        XamlPanel.IsVisible = opening;
        DesignerBody.InputTransparent = opening;
        if (opening && !_xamlDirty)
        {
            RefreshXaml();
        }
    }

    private void OnRefreshXamlClicked(object? sender, EventArgs e) => RefreshXaml();

    private void OnApplyXamlClicked(object? sender, EventArgs e)
    {
        XamlReadResult result = _xamlWorkspace.Parse(XamlEditor.Text ?? string.Empty);
        if (!result.Success || result.Document is null)
        {
            XamlDiagnostic diagnostic = result.Diagnostics.First();
            XamlStatusLabel.Text = diagnostic.Line is null
                ? diagnostic.Message
                : $"Line {diagnostic.Line}, column {diagnostic.Column}: {diagnostic.Message}";
            XamlStatusLabel.TextColor = Color.FromArgb("#F87171");
            return;
        }

        _workspace.ReplaceDocument(result.Document);
        _xamlDirty = false;
        XamlStatusLabel.Text = "Applied";
        XamlStatusLabel.TextColor = Color.FromArgb("#86EFAC");
    }

    private void OnXamlTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_updatingXaml)
        {
            _xamlDirty = true;
            XamlStatusLabel.Text = "Modified";
            XamlStatusLabel.TextColor = Color.FromArgb("#FDE68A");
        }
    }

    private async void OnLoadControlsClicked(object? sender, EventArgs e)
    {
        FileResult? result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a MAUI control assembly"
        });
        if (result is null)
        {
            return;
        }

        try
        {
            ExtensionLoadResult loaded = _extensionLoader.Load(result.FullPath);
            SelectionLabel.Text = $"{loaded.AssemblyName}: {loaded.ControlsAdded} controls added";
            SelectionLabel.TextColor = Color.FromArgb("#86EFAC");
        }
        catch (FileNotFoundException exception)
        {
            ShowPropertyError(exception.Message);
        }
        catch (BadImageFormatException exception)
        {
            ShowPropertyError(exception.Message);
        }
        catch (FileLoadException exception)
        {
            ShowPropertyError(exception.Message);
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RebuildDesigner);

    private void OnSelectionChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RebuildDesigner);

    private void OnInteractionChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(_materializer.UpdateInteraction);

    private void OnCatalogChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => ApplyToolboxFilter(ToolboxSearch.Text ?? string.Empty));

    private void ApplyToolboxFilter(string search)
    {
        IEnumerable<ControlDescriptor> matches = _catalog.Controls;
        if (!string.IsNullOrWhiteSpace(search))
        {
            matches = matches.Where(descriptor =>
                descriptor.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                descriptor.RuntimeType.FullName?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
        }

        _toolboxItems.Clear();
        foreach (ControlDescriptor descriptor in matches)
        {
            _toolboxItems.Add(descriptor);
        }

        ControlCountLabel.Text = _toolboxItems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void RebuildDesigner()
    {
        CanvasHost.Content = _materializer.Materialize(_workspace.Session.Current);
        UndoButton.IsEnabled = _workspace.Session.CanUndo;
        RedoButton.IsEnabled = _workspace.Session.CanRedo;
        RebuildHierarchy();
        RebuildPropertyPanel();
        if (!_xamlDirty)
        {
            RefreshXaml();
        }
    }

    private void RefreshXaml()
    {
        _updatingXaml = true;
        XamlEditor.Text = _xamlWorkspace.Write(_workspace.Session.Current);
        _updatingXaml = false;
        _xamlDirty = false;
        XamlStatusLabel.Text = "Synchronized";
        XamlStatusLabel.TextColor = Color.FromArgb("#858DA2");
    }

    private void RebuildHierarchy()
    {
        _hierarchyItems.Clear();
        AddHierarchyNode(_workspace.Session.Current.Root, 0);
    }

    private void AddHierarchyNode(DesignerNode node, int depth)
    {
        string displayName = _catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor)
            ? descriptor?.DisplayName ?? node.ControlType.XamlName
            : node.ControlType.XamlName;
        _hierarchyItems.Add(new HierarchyItem(
            node.Id,
            displayName,
            node.Id.Value,
            new Thickness(depth * 14, 0, 0, 6),
            node.Id == _workspace.SelectedId));
        foreach (DesignerNode child in node.Children)
        {
            AddHierarchyNode(child, depth + 1);
        }
    }

    private void ShowToolbox(bool show)
    {
        ToolboxSearch.IsVisible = show;
        ToolboxList.IsVisible = show;
        HierarchyList.IsVisible = !show;
        ToolboxTabButton.BackgroundColor = show ? Color.FromArgb("#7C5CFF") : Color.FromArgb("#202431");
        HierarchyTabButton.BackgroundColor = show ? Color.FromArgb("#202431") : Color.FromArgb("#7C5CFF");
    }

    private void RebuildPropertyPanel()
    {
        PropertyPanel.Clear();
        DesignerNode? selected = _workspace.Session.Current.Find(_workspace.SelectedId);
        if (selected is null || !_catalog.TryGet(selected.ControlType, out ControlDescriptor? descriptor) || descriptor is null)
        {
            SelectionLabel.Text = "No selection";
            return;
        }

        SelectionLabel.Text = $"{descriptor.DisplayName}  /  {selected.Id}";
        string filter = PropertySearch.Text?.Trim() ?? string.Empty;
        IEnumerable<IGrouping<string, PropertyDescriptor>> groups = descriptor.Properties
            .Where(IsEditableProperty)
            .Where(property =>
                filter.Length == 0 ||
                property.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(PropertyPriority)
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .Take(80)
            .GroupBy(PropertyGroup);
        foreach (IGrouping<string, PropertyDescriptor> group in groups)
        {
            PropertyPanel.Add(new Label
            {
                Text = group.Key.ToUpperInvariant(),
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#7C5CFF"),
                Margin = new Thickness(0, 6, 0, 0)
            });
            foreach (PropertyDescriptor property in group)
            {
                string? value = selected.Properties.TryGetValue(property.Name, out DesignerValue? designerValue)
                    ? designerValue.Text
                    : null;
                var context = new PropertyEditorContext(
                    property,
                    value,
                    newValue => CommitProperty(property, newValue),
                    ShowPropertyError);
                if (!_propertyEditors.TryCreate(context, out View? editor) || editor is null)
                {
                    continue;
                }

                PropertyPanel.Add(new VerticalStackLayout
                {
                    Spacing = 3,
                    Children =
                    {
                        new Label
                        {
                            Text = property.Name,
                            FontSize = 10,
                            TextColor = Color.FromArgb("#AEB5C7")
                        },
                        editor
                    }
                });
            }
        }
    }

    private void ShowPropertyError(string message)
    {
        SelectionLabel.Text = message;
        SelectionLabel.TextColor = Color.FromArgb("#F87171");
    }

    private void CommitProperty(PropertyDescriptor property, string? text)
    {
        DesignerNode? selected = _workspace.Session.Current.Find(_workspace.SelectedId);
        if (selected is null)
        {
            return;
        }

        DesignerValue? oldValue = selected.Properties.GetValueOrDefault(property.Name);
        DesignerValue? newValue = string.IsNullOrWhiteSpace(text) ? null : DesignerValue.Literal(text);
        if (oldValue == newValue)
        {
            return;
        }

        if (newValue is not null &&
            !DesignerValueConverter.TryConvert(newValue.Text, property.ValueType, out _))
        {
            SelectionLabel.Text = $"{property.Name}: invalid {property.ValueType.Name}";
            SelectionLabel.TextColor = Color.FromArgb("#F87171");
            return;
        }

        SelectionLabel.TextColor = Color.FromArgb("#798196");
        _workspace.Session.Execute(new SetPropertyCommand(selected.Id, property.Name, newValue));
    }

    private static bool IsEditableProperty(PropertyDescriptor property)
    {
        Type type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;
        return !property.IsReadOnly &&
            (type == typeof(string) ||
             type == typeof(bool) ||
             type == typeof(RowDefinitionCollection) ||
             type == typeof(ColumnDefinitionCollection) ||
             type.IsEnum ||
             type.IsPrimitive ||
             type == typeof(decimal) ||
             TypeDescriptorSupportsString(type));
    }

    private static bool TypeDescriptorSupportsString(Type type) =>
        System.ComponentModel.TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));

    private static int PropertyPriority(PropertyDescriptor property) =>
        property.Name switch
        {
            "Text" or "Content" or "Source" or "ItemsSource" => 0,
            "WidthRequest" or "HeightRequest" or "Margin" or "Padding" => 10,
            "HorizontalOptions" or "VerticalOptions" or "RowDefinitions" or "ColumnDefinitions" => 20,
            "Background" or "BackgroundColor" or "TextColor" or "FontSize" or "FontAttributes" => 30,
            "IsVisible" or "IsEnabled" or "Opacity" => 40,
            _ => 100
        };

    private static string PropertyGroup(PropertyDescriptor property) =>
        property.Name switch
        {
            "Text" or "Content" or "Source" or "ItemsSource" or "Placeholder" => "Content",
            "WidthRequest" or "HeightRequest" or "MinimumWidthRequest" or "MinimumHeightRequest" or
                "MaximumWidthRequest" or "MaximumHeightRequest" or "Margin" or "Padding" or
                "HorizontalOptions" or "VerticalOptions" or "RowDefinitions" or "ColumnDefinitions" or
                "RowSpacing" or "ColumnSpacing" or "Spacing" => "Layout",
            "Background" or "BackgroundColor" or "TextColor" or "FontSize" or "FontFamily" or
                "FontAttributes" or "Opacity" or "CornerRadius" or "BorderColor" => "Appearance",
            "IsVisible" or "IsEnabled" or "InputTransparent" or "CascadeInputTransparent" => "Behavior",
            _ => "Advanced"
        };
}
