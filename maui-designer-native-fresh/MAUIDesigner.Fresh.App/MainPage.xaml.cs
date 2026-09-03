using System.Collections.ObjectModel;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.PropertyEditing;
using MAUIDesigner.Fresh.App.Rendering;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.App.Xaml;
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
    private readonly ObservableCollection<ControlDescriptor> _toolboxItems = [];
    private readonly ObservableCollection<HierarchyItem> _hierarchyItems = [];
    private bool _updatingXaml;
    private bool _xamlDirty;

    public MainPage(
        IControlCatalog catalog,
        DesignerWorkspace workspace,
        ControlMaterializer materializer,
        PropertyEditorRegistry propertyEditors,
        AssemblyExtensionLoader extensionLoader,
        XamlWorkspace xamlWorkspace)
    {
        InitializeComponent();
        _catalog = catalog;
        _workspace = workspace;
        _materializer = materializer;
        _propertyEditors = propertyEditors;
        _extensionLoader = extensionLoader;
        _xamlWorkspace = xamlWorkspace;
        ToolboxList.ItemsSource = _toolboxItems;
        HierarchyList.ItemsSource = _hierarchyItems;
        _workspace.Session.Changed += OnDocumentChanged;
        _workspace.SelectionChanged += OnSelectionChanged;
        _workspace.InteractionChanged += OnInteractionChanged;
        _catalog.Changed += OnCatalogChanged;
        ApplyToolboxFilter(string.Empty);
        ShowToolbox(show: true);
        RebuildDesigner();
        RefreshXaml();
    }

    private void OnToolboxSearchChanged(object? sender, TextChangedEventArgs e) =>
        ApplyToolboxFilter(e.NewTextValue ?? string.Empty);

    private void OnToolboxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ControlDescriptor descriptor)
        {
            return;
        }

        _workspace.Add(descriptor);
        ToolboxList.SelectedItem = null;
    }

    private void OnToolboxDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is not DragGestureRecognizer { BindingContext: ControlDescriptor descriptor })
        {
            e.Cancel = true;
            return;
        }

        e.Data.Properties[ControlMaterializer.ToolboxControlPayload] = descriptor;
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
        MainThread.BeginInvokeOnMainThread(RebuildDesigner);

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
        foreach (PropertyDescriptor property in descriptor.Properties.Where(IsEditableProperty).Take(80))
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
             type.IsEnum ||
             type.IsPrimitive ||
             type == typeof(decimal) ||
             TypeDescriptorSupportsString(type));
    }

    private static bool TypeDescriptorSupportsString(Type type) =>
        System.ComponentModel.TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
}
