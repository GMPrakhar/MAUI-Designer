using System.Collections.ObjectModel;
using System.Diagnostics;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Controls;
using MAUIDesigner.Fresh.App.PropertyEditing;
using MAUIDesigner.Fresh.App.Preview;
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
    private readonly IRuntimePreviewService _runtimePreview;
    private bool _updatingXaml;
    private bool _xamlDirty;
    private string _lastSerializedXaml = string.Empty;
    private bool _viewportInitialized;
    private bool _nextDocumentChangeIsIncremental;
    private bool _applyingLiveXaml;
    private bool _renderScheduled;
    private bool _pendingDocumentChange;
    private bool _pendingFullRebuild;
    private bool _pendingSelectionRefresh;
    private bool _pendingSuppressXamlWriteback;
    private bool _hierarchyDirty = true;
    private int _xamlRevision;
    private int _busyOperations;
    private CancellationTokenSource? _xamlSyncCancellation;
#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement? _platformRoot;
#endif

    public MainPage(
        IControlCatalog catalog,
        DesignerWorkspace workspace,
        ControlMaterializer materializer,
        PropertyEditorRegistry propertyEditors,
        AssemblyExtensionLoader extensionLoader,
        XamlWorkspace xamlWorkspace,
        DesignerViewportState viewport,
        IRuntimePreviewService runtimePreview)
    {
        InitializeComponent();
        HierarchyList.ItemTemplate = new DataTemplate(() =>
        {
            var host = new ContentView();
            host.BindingContextChanged += (_, _) =>
            {
                host.Content = host.BindingContext is HierarchyItem item
                    ? CreateHierarchyRow(item.Node, item.Depth, item.IsLast)
                    : null;
            };
            return host;
        });
        _catalog = catalog;
        _workspace = workspace;
        _materializer = materializer;
        _propertyEditors = propertyEditors;
        _extensionLoader = extensionLoader;
        _xamlWorkspace = xamlWorkspace;
        _viewport = viewport;
        _runtimePreview = runtimePreview;
        _gridDrawable = new CanvasGridDrawable(viewport);
        _rulerDrawable = new CanvasRulerDrawable(viewport);
        CanvasGridOverlay.Drawable = _gridDrawable;
        CanvasRulerOverlay.Drawable = _rulerDrawable;
        DevicePicker.ItemDisplayBinding = new Binding(nameof(DevicePreset.Name));
        DevicePicker.ItemsSource = _viewport.Devices.ToList();
        DevicePicker.SelectedItem = _viewport.SelectedDevice;
        GridSizeStepper.Value = _viewport.GridSize;
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

    protected override void OnHandlerChanged()
    {
#if WINDOWS
        if (_platformRoot is not null)
        {
            _platformRoot.RemoveHandler(
                Microsoft.UI.Xaml.UIElement.KeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler(OnNativeKeyDown));
        }
#endif
        base.OnHandlerChanged();
#if WINDOWS
        if (Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement root)
        {
            _platformRoot = root;
            root.AddHandler(
                Microsoft.UI.Xaml.UIElement.KeyDownEvent,
                new Microsoft.UI.Xaml.Input.KeyEventHandler(OnNativeKeyDown),
                true);
        }
#endif
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
        CanvasFrame.BackgroundColor = Colors.White;
        CanvasViewport.BackgroundColor = Color.FromArgb("#EEF2F7");
        ZoomLabel.Text = $"{Math.Round(_viewport.Zoom * 100)}%";
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
        _materializer.RefreshGridTrackOverlays();
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

    private void OnUndoClicked(object? sender, EventArgs e) => _workspace.Session.Undo();

    private void OnRedoClicked(object? sender, EventArgs e) => _workspace.Session.Redo();

    private void OnDeleteClicked(object? sender, EventArgs e) => _workspace.DeleteSelection();

    private void OnCutClicked(object? sender, EventArgs e) => _workspace.CutSelection();

    private void OnCopyClicked(object? sender, EventArgs e) => _workspace.CopySelection();

    private void OnPasteClicked(object? sender, EventArgs e) => _ = _workspace.Paste();

    private void OnDuplicateClicked(object? sender, EventArgs e) =>
        _ = _workspace.DuplicateSelection();

    private async void OnRunPreviewClicked(object? sender, EventArgs e)
    {
        SetBusy(true, "Opening preview...");
        try
        {
            DevicePreset device = _viewport.SelectedDevice;
            await _runtimePreview.OpenAsync(
                _workspace.Session.Current,
                new RuntimePreviewOptions(device.Name, device.Width, device.Height));
        }
        catch (InvalidOperationException exception)
        {
            ShowPropertyError(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnToggleXamlClicked(object? sender, EventArgs e)
    {
        bool opening = !XamlPanel.IsVisible;
        XamlPanel.IsVisible = opening;
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
            XamlStatusLabel.TextColor = Color.FromArgb("#DC2626");
            return;
        }

        _workspace.ReplaceDocument(result.Document);
        _xamlDirty = false;
        XamlStatusLabel.Text = "Applied";
        XamlStatusLabel.TextColor = Color.FromArgb("#16A34A");
    }

    private async void OnXamlTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingXaml ||
            string.Equals(
                XamlTextIdentity.Normalize(e.NewTextValue),
                _lastSerializedXaml,
                StringComparison.Ordinal))
        {
            return;
        }

        _xamlDirty = true;
        DesignerDocument sourceDocument = _workspace.Session.Current;
        int revision = Interlocked.Increment(ref _xamlRevision);
        _xamlSyncCancellation?.Cancel();
        _xamlSyncCancellation?.Dispose();
        _xamlSyncCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _xamlSyncCancellation.Token;
        XamlStatusLabel.Text = "Waiting for valid XAML...";
        XamlStatusLabel.TextColor = Color.FromArgb("#64748B");
        XamlBusyIndicator.IsRunning = true;
        XamlBusyIndicator.IsVisible = true;
        try
        {
            await Task.Delay(300, cancellationToken);
            var parseStopwatch = Stopwatch.StartNew();
            string source = XamlEditor.Text ?? string.Empty;
            XamlReadResult result = await Task.Run(
                () => _xamlWorkspace.Parse(source),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (revision != _xamlRevision)
            {
                return;
            }

            if (!ReferenceEquals(sourceDocument, _workspace.Session.Current))
            {
                _xamlDirty = false;
                RefreshXaml();
                XamlStatusLabel.Text = "Canvas change kept";
                return;
            }

            if (!result.Success || result.Document is null)
            {
                XamlDiagnostic diagnostic = result.Diagnostics.First();
                XamlStatusLabel.Text = diagnostic.Line is null
                    ? diagnostic.Message
                    : $"Line {diagnostic.Line}, column {diagnostic.Column}: {diagnostic.Message}";
                XamlStatusLabel.TextColor = Color.FromArgb("#DC2626");
                return;
            }

            _applyingLiveXaml = true;
            _workspace.ReplaceDocument(result.Document);
            parseStopwatch.Stop();
            ReportPerformance("live-xaml-parse-and-command", parseStopwatch.Elapsed, 100);
            _xamlDirty = false;
            XamlStatusLabel.Text = "Live";
            XamlStatusLabel.TextColor = Color.FromArgb("#16A34A");
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (revision == _xamlRevision)
            {
                _applyingLiveXaml = false;
                XamlBusyIndicator.IsRunning = false;
                XamlBusyIndicator.IsVisible = false;
            }
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
            SelectionLabel.TextColor = Color.FromArgb("#16A34A");
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

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        bool incremental = _nextDocumentChangeIsIncremental;
        bool suppressXamlWriteback = _applyingLiveXaml;
        _nextDocumentChangeIsIncremental = false;
        _pendingDocumentChange = true;
        _pendingFullRebuild |= !incremental;
        _pendingSuppressXamlWriteback |= suppressXamlWriteback;
        if (_busyOperations == 0)
        {
            SetBusy(true, incremental ? "Applying property..." : "Rendering design...");
        }

        ScheduleRender();
    }

    private void OnSelectionChanged(object? sender, EventArgs e) =>
        ScheduleSelectionRefresh();

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

        ControlDescriptor[] visible = matches.ToArray();
        ToolboxItemsHost.Clear();
        foreach (IGrouping<string, ControlDescriptor> category in visible
                     .GroupBy(descriptor => descriptor.Category)
                     .OrderBy(group => CategoryOrder(group.Key))
                     .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            ToolboxItemsHost.Add(new Label
            {
                Text = category.Key.ToUpperInvariant(),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#6D5BD0"),
                Margin = new Thickness(4, 12, 4, 4)
            });
            foreach (ControlDescriptor descriptor in category)
            {
                ToolboxItemsHost.Add(CreateToolboxItem(descriptor));
            }
        }

        ControlCountLabel.Text = visible.Length.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private void RebuildDesigner(bool suppressXamlWriteback = false)
    {
        var stopwatch = Stopwatch.StartNew();
        CanvasHost.Content = _materializer.Materialize(_workspace.Session.Current);
        UndoButton.IsEnabled = _workspace.Session.CanUndo;
        RedoButton.IsEnabled = _workspace.Session.CanRedo;
        _hierarchyDirty = true;
        if (HierarchyList.IsVisible)
        {
            RebuildHierarchy();
        }
        RebuildPropertyPanel();
        if (!_xamlDirty && !suppressXamlWriteback)
        {
            RefreshXaml();
        }

        stopwatch.Stop();
        ReportPerformance("structural-rematerialization", stopwatch.Elapsed, 50);
        if (stopwatch.ElapsedMilliseconds >= 100)
        {
            SelectionLabel.Text = $"Rendered in {stopwatch.ElapsedMilliseconds} ms";
        }
    }

    private void RefreshXaml()
    {
        string xaml = _xamlWorkspace.Write(_workspace.Session.Current);
        _lastSerializedXaml = XamlTextIdentity.Normalize(xaml);
        _updatingXaml = true;
        XamlEditor.Text = xaml;
        Dispatcher.Dispatch(() => _updatingXaml = false);
        _xamlDirty = false;
        XamlStatusLabel.Text = "Synchronized";
        XamlStatusLabel.TextColor = Color.FromArgb("#64748B");
    }

    private void RebuildHierarchy()
    {
        _hierarchyDirty = false;
        var items = new List<HierarchyItem>();
        AddHierarchyItems(_workspace.Session.Current.Root, 0, isLast: true, items);
        HierarchyList.ItemsSource = items;
    }

    private static void AddHierarchyItems(
        DesignerNode node,
        int depth,
        bool isLast,
        List<HierarchyItem> items)
    {
        items.Add(new HierarchyItem(node, depth, isLast));
        for (int index = 0; index < node.Children.Length; index++)
        {
            AddHierarchyItems(
                node.Children[index],
                depth + 1,
                index == node.Children.Length - 1,
                items);
        }
    }

    private Border CreateHierarchyRow(DesignerNode node, int depth, bool isLast)
    {
        string displayName = _catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor)
            ? descriptor?.DisplayName ?? node.ControlType.XamlName
            : node.ControlType.XamlName;

        bool selected = node.Id == _workspace.SelectedId;
        var row = new Border
        {
            AutomationId = $"hierarchy-{node.Id.Value}",
            Margin = new Thickness(depth * 16, 0, 0, 5),
            Padding = new Thickness(8, 6),
            BackgroundColor = selected
                ? Color.FromArgb("#EDE9FE")
                : Colors.White,
            Stroke = selected
                ? Color.FromArgb("#6D5BD0")
                : Color.FromArgb("#DDE3EC"),
            StrokeThickness = selected ? 2 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(7)
            }
        };
        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(18)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(28)),
                new ColumnDefinition(new GridLength(28)),
                new ColumnDefinition(new GridLength(28))
            },
            ColumnSpacing = 3
        };
        content.Add(new Label
        {
            Text = depth == 0 ? "◆" : isLast ? "└" : "├",
            FontSize = 10,
            TextColor = Color.FromArgb("#6D5BD0"),
            VerticalTextAlignment = TextAlignment.Center
        });
        var labels = new VerticalStackLayout { Spacing = 0 };
        labels.Add(new Label
        {
            Text = displayName,
            FontSize = 11,
            FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
            TextColor = Color.FromArgb("#172033")
        });
        labels.Add(new Label
        {
            Text = $"{node.Id.Value}  ·  {node.Children.Length} children",
            FontSize = 9,
            TextColor = Color.FromArgb("#64748B")
        });
        content.Add(labels, 1);
        content.Add(CreateHierarchyAction("↑", "Move up", 2, () =>
        {
            _workspace.Select(node.Id);
            _ = _workspace.MoveSelection(-1);
        }));
        content.Add(CreateHierarchyAction("↓", "Move down", 3, () =>
        {
            _workspace.Select(node.Id);
            _ = _workspace.MoveSelection(1);
        }));
        content.Add(CreateHierarchyAction("×", "Delete", 4, () =>
        {
            _workspace.Select(node.Id);
            _workspace.DeleteSelection();
        }, destructive: true));
        row.Content = content;
        var select = new TapGestureRecognizer();
        select.Tapped += (_, _) =>
        {
            _workspace.Select(node.Id);
            FocusCanvasElement(node.Id);
        };
        row.GestureRecognizers.Add(select);
        AttachHierarchyDragDrop(row, node);
        AttachContextMenu(row, node.Id);
        return row;
    }

    private void AttachHierarchyDragDrop(View row, DesignerNode node)
    {
        if (node.Id != _workspace.Session.Current.Root.Id)
        {
            var drag = new DragGestureRecognizer();
            drag.DragStarting += (_, args) =>
            {
                _workspace.Select(node.Id);
                args.Data.Properties["DesignerElementId"] = node.Id.Value;
            };
            row.GestureRecognizers.Add(drag);
        }

        var drop = new DropGestureRecognizer { AllowDrop = true };
        Color restingColor = node.Id == _workspace.SelectedId
            ? Color.FromArgb("#EDE9FE")
            : Colors.White;
        drop.DragOver += (_, args) =>
        {
            if (TryGetDraggedElement(args.Data, out ElementId sourceId) &&
                sourceId != node.Id)
            {
                args.AcceptedOperation = DataPackageOperation.Copy;
                row.BackgroundColor = Color.FromArgb("#DDD6FE");
                _workspace.SetDropTarget(node.Id);
            }
        };
        drop.DragLeave += (_, _) =>
        {
            row.BackgroundColor = restingColor;
            _workspace.ClearDropTarget();
        };
        drop.Drop += (_, args) =>
        {
            try
            {
                if (TryGetDraggedElement(args.Data, out ElementId sourceId) &&
                    sourceId != node.Id)
                {
                    ReparentFromHierarchy(sourceId, node);
                }
            }
            catch (InvalidOperationException exception)
            {
                ShowPropertyError(exception.Message);
            }
            finally
            {
                row.BackgroundColor = restingColor;
                _workspace.ClearDropTarget();
            }
        };
        row.GestureRecognizers.Add(drop);
    }

    private void ReparentFromHierarchy(ElementId sourceId, DesignerNode target)
    {
        if (_catalog.TryGet(target.ControlType, out ControlDescriptor? descriptor) &&
            descriptor?.AcceptsChildren == true)
        {
            _workspace.Reparent(
                sourceId,
                target.Id,
                new LayoutPlacement(target.Children.Length));
            return;
        }

        DesignerNode? parent = FindParent(_workspace.Session.Current.Root, target.Id);
        if (parent is null)
        {
            return;
        }

        int destinationIndex = parent.Children.IndexOf(target);
        DesignerNode? sourceParent = FindParent(_workspace.Session.Current.Root, sourceId);
        if (sourceParent?.Id == parent.Id &&
            IndexOfChild(sourceParent, sourceId) < destinationIndex)
        {
            destinationIndex--;
        }

        _workspace.Reparent(
            sourceId,
            parent.Id,
            new LayoutPlacement(destinationIndex));
    }

    private static DesignerNode? FindParent(DesignerNode node, ElementId childId)
    {
        foreach (DesignerNode child in node.Children)
        {
            if (child.Id == childId)
            {
                return node;
            }

            DesignerNode? descendantParent = FindParent(child, childId);
            if (descendantParent is not null)
            {
                return descendantParent;
            }
        }

        return null;
    }

    private static int IndexOfChild(DesignerNode parent, ElementId childId)
    {
        for (int index = 0; index < parent.Children.Length; index++)
        {
            if (parent.Children[index].Id == childId)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryGetDraggedElement(
        DataPackage data,
        out ElementId elementId)
    {
        data.Properties.TryGetValue("DesignerElementId", out object? raw);
        return TryCreateElementId(raw, out elementId);
    }

    private static bool TryGetDraggedElement(
        DataPackageView data,
        out ElementId elementId)
    {
        data.Properties.TryGetValue("DesignerElementId", out object? raw);
        return TryCreateElementId(raw, out elementId);
    }

    private static bool TryCreateElementId(object? raw, out ElementId elementId)
    {
        bool valid = raw is string value && !string.IsNullOrWhiteSpace(value);
        elementId = valid ? new ElementId((string)raw!) : default;
        return valid;
    }

    private static Button CreateHierarchyAction(
        string text,
        string description,
        int column,
        Action action,
        bool destructive = false)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 11,
            WidthRequest = 26,
            HeightRequest = 26,
            Padding = 0,
            CornerRadius = 6,
            BackgroundColor = destructive
                ? Color.FromArgb("#FEF2F2")
                : Color.FromArgb("#F1F5F9"),
            TextColor = destructive
                ? Color.FromArgb("#DC2626")
                : Color.FromArgb("#475569")
        };
        SemanticProperties.SetDescription(button, description);
        button.Clicked += (_, _) => action();
        Grid.SetColumn(button, column);
        return button;
    }

    private void FocusCanvasElement(ElementId elementId)
    {
        DesignerNode? node = _workspace.Session.Current.Find(elementId);
        if (node?.Bounds is { } bounds)
        {
            _viewport.CenterOn(
                bounds.X + bounds.Width / 2,
                bounds.Y + bounds.Height / 2,
                CanvasViewport.Width,
                CanvasViewport.Height);
            UpdateViewportVisuals();
        }

        _materializer.UpdateInteraction();
    }

    private void AttachContextMenu(View target, ElementId elementId)
    {
#if WINDOWS
        target.HandlerChanged += (_, _) =>
        {
            if (target.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement native ||
                native.ContextFlyout is not null)
            {
                return;
            }

            var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
            AddContextMenuItem(menu, "Cut", () =>
            {
                _workspace.Select(elementId);
                _workspace.CutSelection();
            });
            AddContextMenuItem(menu, "Copy", () =>
            {
                _workspace.Select(elementId);
                _workspace.CopySelection();
            });
            AddContextMenuItem(menu, "Paste", () =>
            {
                _workspace.Select(elementId);
                _ = _workspace.Paste();
            });
            AddContextMenuItem(menu, "Duplicate", () =>
            {
                _workspace.Select(elementId);
                _ = _workspace.DuplicateSelection();
            });
            AddContextMenuItem(menu, "Move up", () =>
            {
                _workspace.Select(elementId);
                _ = _workspace.MoveSelection(-1);
            });
            AddContextMenuItem(menu, "Move down", () =>
            {
                _workspace.Select(elementId);
                _ = _workspace.MoveSelection(1);
            });
            AddContextMenuItem(menu, "Delete", () =>
            {
                _workspace.Select(elementId);
                _workspace.DeleteSelection();
            });
            native.ContextFlyout = menu;
        };
#endif
    }

#if WINDOWS
    private static void AddContextMenuItem(
        Microsoft.UI.Xaml.Controls.MenuFlyout menu,
        string text,
        Action action)
    {
        var item = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
#endif

    private void ShowToolbox(bool show)
    {
        ToolboxSearch.IsVisible = show;
        ToolboxList.IsVisible = show;
        HierarchyList.IsVisible = !show;
        ToolboxTabButton.BackgroundColor = show ? Color.FromArgb("#6D5BD0") : Color.FromArgb("#EEF1F6");
        ToolboxTabButton.TextColor = show ? Colors.White : Color.FromArgb("#475569");
        HierarchyTabButton.BackgroundColor = show ? Color.FromArgb("#EEF1F6") : Color.FromArgb("#6D5BD0");
        HierarchyTabButton.TextColor = show ? Color.FromArgb("#475569") : Colors.White;
        if (!show && _hierarchyDirty)
        {
            RebuildHierarchy();
        }
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
                TextColor = Color.FromArgb("#6D5BD0"),
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
                    newValue => CommitProperty(selected.Id, property, newValue),
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
                            TextColor = Color.FromArgb("#475569")
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
        SelectionLabel.TextColor = Color.FromArgb("#DC2626");
    }

    private void CommitProperty(
        ElementId elementId,
        PropertyDescriptor property,
        string? text)
    {
        DesignerNode? selected = _workspace.Session.Current.Find(elementId);
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
            SelectionLabel.TextColor = Color.FromArgb("#DC2626");
            return;
        }

        SelectionLabel.TextColor = Color.FromArgb("#64748B");
        var stopwatch = Stopwatch.StartNew();
        bool incremental = newValue is not null &&
            _materializer.TryApplyProperty(selected.Id, property.Name, newValue);
        _nextDocumentChangeIsIncremental = incremental;
        _workspace.Session.Execute(new SetPropertyCommand(selected.Id, property.Name, newValue));
        stopwatch.Stop();
        ReportPerformance("literal-property-commit", stopwatch.Elapsed, 16);
        SelectionLabel.Text = $"{property.Name} updated";
        SelectionLabel.TextColor = Color.FromArgb("#16A34A");
    }

    private ToolboxItemView CreateToolboxItem(ControlDescriptor descriptor)
    {
        var item = new ToolboxItemView
        {
            AutomationId = descriptor.AutomationId,
            Descriptor = descriptor,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(10, 8),
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#DDE3EC"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(8)
            },
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(32)),
                    new ColumnDefinition(GridLength.Star)
                },
                Children =
                {
                    new Border
                    {
                        WidthRequest = 26,
                        HeightRequest = 26,
                        BackgroundColor = Color.FromArgb("#EEF0FF"),
                        StrokeThickness = 0,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                        {
                            CornerRadius = new CornerRadius(7)
                        },
                        Content = new Label
                        {
                            Text = "+",
                            TextColor = Color.FromArgb("#6554C0"),
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        }
                    },
                    new Label
                    {
                        Text = descriptor.DisplayName,
                        TextColor = Color.FromArgb("#172033"),
                        FontSize = 12,
                        VerticalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(8, 0, 0, 0)
                    }
                }
            }
        };
        ((BindableObject)((Microsoft.Maui.Controls.Grid)item.Content).Children[1])
            .SetValue(Microsoft.Maui.Controls.Grid.ColumnProperty, 1);
        item.ItemDragUpdated += OnToolboxDragUpdated;
        item.ItemTapped += OnToolboxItemTapped;
        return item;
    }

    private static int CategoryOrder(string category) =>
        category switch
        {
            "Layouts" => 0,
            "Input" => 1,
            "Display" => 2,
            "Data and collections" => 3,
            _ => 10
        };

    private void ScheduleSelectionRefresh()
    {
        _pendingSelectionRefresh = true;
        ScheduleRender();
    }

    private void ScheduleRender()
    {
        if (_renderScheduled)
        {
            return;
        }

        _renderScheduled = true;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _renderScheduled = false;
            bool documentChanged = _pendingDocumentChange;
            bool fullRebuild = _pendingFullRebuild;
            bool selectionChanged = _pendingSelectionRefresh;
            bool suppressXamlWriteback = _pendingSuppressXamlWriteback;
            _pendingDocumentChange = false;
            _pendingFullRebuild = false;
            _pendingSelectionRefresh = false;
            _pendingSuppressXamlWriteback = false;
            try
            {
                UndoButton.IsEnabled = _workspace.Session.CanUndo;
                RedoButton.IsEnabled = _workspace.Session.CanRedo;
                if (fullRebuild)
                {
                    RebuildDesigner(suppressXamlWriteback);
                }
                else
                {
                    if (documentChanged && !_xamlDirty && !suppressXamlWriteback)
                    {
                        RefreshXaml();
                    }

                    if (selectionChanged)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        _materializer.UpdateInteraction();
                        stopwatch.Stop();
                        ReportPerformance("selection-chrome", stopwatch.Elapsed, 5);
                        _hierarchyDirty = true;
                        if (HierarchyList.IsVisible)
                        {
                            RebuildHierarchy();
                        }
                        SetBusy(true, "Loading properties...");
                        Dispatcher.DispatchDelayed(
                            TimeSpan.FromMilliseconds(1),
                            () =>
                            {
                                try
                                {
                                    RebuildPropertyPanel();
                                }
                                finally
                                {
                                    SetBusy(false);
                                }
                            });
                    }
                }
            }
            finally
            {
                if (documentChanged)
                {
                    SetBusy(false);
                }
            }
        });
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busyOperations = Math.Max(0, _busyOperations + (busy ? 1 : -1));
        bool visible = _busyOperations > 0;
        BusyOverlay.IsVisible = visible;
        BusyIndicator.IsRunning = visible;
        if (message is not null)
        {
            BusyLabel.Text = message;
        }
    }

    [Conditional("DEBUG")]
    private static void ReportPerformance(
        string operation,
        TimeSpan elapsed,
        int budgetMilliseconds)
    {
        string outcome = elapsed.TotalMilliseconds <= budgetMilliseconds ? "PASS" : "MISS";
        Console.WriteLine(
            $"[DesignerPerformance] {outcome} {operation} " +
            $"{elapsed.TotalMilliseconds:F2} ms / {budgetMilliseconds} ms");
    }

    private sealed record HierarchyItem(
        DesignerNode Node,
        int Depth,
        bool IsLast);

#if WINDOWS
    private void OnNativeKeyDown(
        object sender,
        Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_platformRoot?.XamlRoot is null ||
            Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(_platformRoot.XamlRoot) is
                Microsoft.UI.Xaml.Controls.TextBox or
                Microsoft.UI.Xaml.Controls.RichEditBox or
                Microsoft.UI.Xaml.Controls.PasswordBox)
        {
            return;
        }

        bool control = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool alt = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (control)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.C:
                    _workspace.CopySelection();
                    e.Handled = true;
                    return;
                case Windows.System.VirtualKey.X:
                    _workspace.CutSelection();
                    e.Handled = true;
                    return;
                case Windows.System.VirtualKey.V:
                    _ = _workspace.Paste();
                    e.Handled = true;
                    return;
                case Windows.System.VirtualKey.D:
                    _ = _workspace.DuplicateSelection();
                    e.Handled = true;
                    return;
            }
        }

        if (alt && e.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down)
        {
            _ = _workspace.MoveSelection(
                e.Key == Windows.System.VirtualKey.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Delete)
        {
            _workspace.DeleteSelection();
            e.Handled = true;
        }
    }
#endif

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
