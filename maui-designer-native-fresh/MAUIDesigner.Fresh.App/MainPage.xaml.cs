using System.Collections.ObjectModel;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Rendering;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App;

public partial class MainPage : ContentPage
{
    private readonly IControlCatalog _catalog;
    private readonly DesignerWorkspace _workspace;
    private readonly ControlMaterializer _materializer;
    private readonly ObservableCollection<ControlDescriptor> _toolboxItems = [];

    public MainPage(
        IControlCatalog catalog,
        DesignerWorkspace workspace,
        ControlMaterializer materializer)
    {
        InitializeComponent();
        _catalog = catalog;
        _workspace = workspace;
        _materializer = materializer;
        ToolboxList.ItemsSource = _toolboxItems;
        _workspace.Session.Changed += OnDocumentChanged;
        _workspace.SelectionChanged += OnSelectionChanged;
        _workspace.InteractionChanged += OnInteractionChanged;
        ApplyToolboxFilter(string.Empty);
        RebuildDesigner();
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

    private void OnUndoClicked(object? sender, EventArgs e) => _workspace.Session.Undo();

    private void OnRedoClicked(object? sender, EventArgs e) => _workspace.Session.Redo();

    private void OnDeleteClicked(object? sender, EventArgs e) => _workspace.DeleteSelection();

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RebuildDesigner);

    private void OnSelectionChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RebuildDesigner);

    private void OnInteractionChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(RebuildDesigner);

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
        RebuildPropertyPanel();
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
            var editor = new Entry
            {
                AutomationId = $"property-{property.Name}",
                FontSize = 11,
                HeightRequest = 34,
                Placeholder = property.ValueType.Name,
                Text = selected.Properties.TryGetValue(property.Name, out DesignerValue? value) ? value.Text : string.Empty
            };
            editor.Completed += (_, _) => CommitProperty(property, editor.Text);
            editor.Unfocused += (_, _) => CommitProperty(property, editor.Text);

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
