using System.Reflection;
using System.Runtime.CompilerServices;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.App.Viewport;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Rendering;

public sealed class ControlMaterializer
{
    private readonly IControlCatalog _catalog;
    private readonly Dictionary<ElementId, Border> _outlines = [];
    private readonly Dictionary<ElementId, View> _views = [];
    private readonly Dictionary<ElementId, Grid> _chromes = [];
    private readonly Dictionary<ElementId, View> _moveHandles = [];
    private readonly Dictionary<ElementId, View> _resizeHandles = [];
    private readonly Dictionary<ElementId, (View View, ILayoutAdapter Adapter)> _targets = [];
    private readonly List<Action> _gridTrackUpdates = [];
    private readonly ConditionalWeakTable<Microsoft.UI.Xaml.FrameworkElement, object>
        _contextMenuTargets = new();
    private readonly LayoutAdapterRegistry _layoutAdapters = new();
    private readonly DesignerWorkspace _workspace;
    private readonly DesignerViewportState _viewport;
    private View? _activeDropPreview;
    private ManualDragState? _manualDrag;

    public ControlMaterializer(
        IControlCatalog catalog,
        DesignerWorkspace workspace,
        DesignerViewportState viewport)
    {
        _catalog = catalog;
        _workspace = workspace;
        _viewport = viewport;
    }

    public View Materialize(DesignerDocument document)
    {
        _outlines.Clear();
        _views.Clear();
        _chromes.Clear();
        _moveHandles.Clear();
        _resizeHandles.Clear();
        _targets.Clear();
        _gridTrackUpdates.Clear();
        _activeDropPreview = null;
        _manualDrag = null;
        return Build(document.Root, isRoot: true);
    }

    public void RefreshGridTrackOverlays()
    {
        foreach (Action update in _gridTrackUpdates)
        {
            update();
        }
    }

    public void UpdateInteraction()
    {
        RemoveActiveDropPreview();
        foreach ((ElementId id, Grid chrome) in _chromes)
        {
            bool selected = _workspace.SelectedIds.Contains(id);
            bool highlighted = selected || id == _workspace.DropTargetId;
            if (highlighted)
            {
                Border outline = EnsureOutline(chrome, id);
                UpdateOutline(outline, id);
            }
            else if (_outlines.Remove(id, out Border? outline))
            {
                chrome.Remove(outline);
            }

            if (id == _workspace.SelectedId)
            {
                EnsureSelectionHandles(chrome, id);
            }
            else
            {
                RemoveSelectionHandles(chrome, id);
            }
        }

        if (_workspace.DropTargetId is not ElementId targetId ||
            _workspace.DropPlacement is not LayoutPlacement placement ||
            !_targets.TryGetValue(targetId, out (View View, ILayoutAdapter Adapter) target))
        {
            return;
        }

        _activeDropPreview = target.Adapter.AddDropPreview(target.View, placement);
    }

    public bool TryApplyProperty(
        ElementId elementId,
        string propertyName,
        DesignerValue? designerValue)
    {
        if (designerValue is null ||
            !_views.TryGetValue(elementId, out View? view) ||
            propertyName is nameof(Grid.RowDefinitions) or nameof(Grid.ColumnDefinitions))
        {
            return false;
        }

        DesignerNode? node = _workspace.Session.Current.Find(elementId);
        if (node is null ||
            !_catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) ||
            descriptor is null)
        {
            return false;
        }

        try
        {
            return TryApplyPropertyValue(view, descriptor, propertyName, designerValue);
        }
        catch (Exception exception) when (IsRecoverableMaterializationFailure(exception))
        {
            return false;
        }
    }

    public IReadOnlyList<ElementId> FindElementsInside(RectD windowBounds)
    {
        if (windowBounds.Width <= 0 || windowBounds.Height <= 0)
        {
            return [];
        }

        return _chromes
            .Where(pair =>
                TryGetWindowBounds(pair.Value, out RectD bounds) &&
                MarqueeSelectionPolicy.Contains(windowBounds, bounds))
            .Select(pair => pair.Key)
            .ToArray();
    }

    public void BeginManualDrag(View source, ElementId? movingId)
    {
        if (!TryGetWindowBounds(source, out RectD sourceBounds))
        {
            _manualDrag = null;
            return;
        }

        ManualDropTarget[] targets = _targets
            .Where(target => _workspace.CanAcceptChild(target.Key, movingId))
            .Select(target => TryGetWindowBounds(target.Value.View, out RectD bounds)
                ? new ManualDropTarget(
                    target.Key,
                    target.Value.View,
                    target.Value.Adapter,
                    bounds)
                : null)
            .OfType<ManualDropTarget>()
            .OrderBy(target => target.Bounds.Area)
            .ToArray();
        _manualDrag = new ManualDragState(
            new PointD(
                sourceBounds.X + sourceBounds.Width / 2,
                sourceBounds.Y + sourceBounds.Height / 2),
            targets);
    }

    public ElementId? UpdateManualDrag(double totalX, double totalY)
    {
        if (_manualDrag is not ManualDragState drag)
        {
            return null;
        }

        var pointer = new PointD(drag.Start.X + totalX, drag.Start.Y + totalY);
        ManualDropTarget? target = drag.Targets.FirstOrDefault(candidate =>
            candidate.Bounds.Contains(pointer));
        DesignerNode? parentNode = target is null
            ? null
            : _workspace.Session.Current.Find(target.Id);
        if (target is null || parentNode is null)
        {
            _workspace.ClearDropTarget();
            return null;
        }

        LayoutPlacement placement = target.Adapter.ResolveDrop(
            target.View,
            parentNode,
            new PointD(
                (pointer.X - target.Bounds.X) / _viewport.Zoom,
                (pointer.Y - target.Bounds.Y) / _viewport.Zoom));
        if (placement.Bounds is RectD bounds)
        {
            placement = placement with
            {
                Bounds = bounds with
                {
                    X = _viewport.Snap(bounds.X),
                    Y = _viewport.Snap(bounds.Y)
                }
            };
        }
        _workspace.SetDropTarget(target.Id, placement);
        return target.Id;
    }

    public void CompleteManualToolboxDrag(ControlDescriptor descriptor)
    {
        _manualDrag = null;
        if (_workspace.DropTargetId is ElementId targetId &&
            _workspace.DropPlacement is LayoutPlacement placement)
        {
            _workspace.Add(descriptor, targetId, placement);
        }

        _workspace.ClearDropTarget();
    }

    public void CancelManualDrag()
    {
        _manualDrag = null;
        _workspace.ClearDropTarget();
    }

    private View Build(DesignerNode node, bool isRoot)
    {
        if (!_catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) || descriptor is null)
        {
            return CreateUnknownControl(node);
        }

        try
        {
            View view = _catalog.Create(node.ControlType);
            ApplyProperties(view, descriptor, node);
            if (!isRoot && DesignerInputPolicy.SuppressRuntimeInput(descriptor))
            {
                view.InputTransparent = true;
            }

            if (!node.Properties.ContainsKey(nameof(VisualElement.AutomationId)))
            {
                view.AutomationId = $"designer-{node.Id.Value}";
            }

            _views[node.Id] = view;
            ILayoutAdapter? layoutAdapter = descriptor.AcceptsChildren
                ? _layoutAdapters.Resolve(descriptor)
                : null;
            foreach (DesignerNode childNode in node.Children)
            {
                View child = Build(childNode, isRoot: false);
                if (childNode.ParentPropertyName is string propertyName)
                {
                    SetVisualProperty(view, propertyName, child);
                }
                else
                {
                    layoutAdapter!.AddChild(view, child, childNode);
                }
            }

            EnsureDesignSize(view, descriptor);
            if (layoutAdapter is not null)
            {
                _targets[node.Id] = (view, layoutAdapter);
            }

            if (isRoot)
            {
                return view is Grid rootGrid
                    ? CreateGridTrackSurface(rootGrid)
                    : view;
            }

            return CreateChrome(view, node);
        }
        catch (Exception exception) when (IsRecoverableMaterializationFailure(exception))
        {
            return CreateUnavailableControl(node, exception.InnerException?.Message ?? exception.Message);
        }
    }

    private View CreateChrome(View content, DesignerNode node)
    {
        var chrome = new Grid
        {
            MinimumWidthRequest = 24,
            MinimumHeightRequest = 24,
            AutomationId = $"chrome-{node.Id.Value}"
        };
        _chromes[node.Id] = chrome;
        chrome.Add(content);
        if (content is Grid gridContent)
        {
            AddGridTrackOverlay(chrome, gridContent);
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
#if WINDOWS
            if (IsControlPressed())
            {
                _workspace.ToggleSelection(node.Id);
                return;
            }
#endif
            _workspace.Select(node.Id);
        };
        chrome.GestureRecognizers.Add(tap);
        AttachContextMenu(chrome, node.Id);

        var reparent = new PanGestureRecognizer();
        reparent.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Started)
            {
                BeginManualDrag(chrome, node.Id);
            }
            else if (args.StatusType == GestureStatus.Running)
            {
                _ = UpdateManualDrag(args.TotalX, args.TotalY);
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                _manualDrag = null;
                if (_workspace.DropTargetId is ElementId targetId &&
                    _workspace.DropPlacement is LayoutPlacement placement)
                {
                    _workspace.Reparent(node.Id, targetId, placement);
                }
                else
                {
                    _workspace.ClearDropTarget();
                }
            }
            else if (args.StatusType == GestureStatus.Canceled)
            {
                CancelManualDrag();
            }
        };
        chrome.GestureRecognizers.Add(reparent);

        if (_workspace.SelectedIds.Contains(node.Id))
        {
            _ = EnsureOutline(chrome, node.Id);
            if (node.Id == _workspace.SelectedId)
            {
                EnsureSelectionHandles(chrome, node.Id);
            }
        }

        return chrome;
    }

    private Border EnsureOutline(Grid chrome, ElementId id)
    {
        if (_outlines.TryGetValue(id, out Border? existing))
        {
            return existing;
        }

        var outline = new Border
        {
            InputTransparent = true,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(4)
            }
        };
        UpdateOutline(outline, id);
        _outlines[id] = outline;
        chrome.Add(outline);
        return outline;
    }

    private void EnsureSelectionHandles(Grid chrome, ElementId id)
    {
        if (_moveHandles.ContainsKey(id) || _resizeHandles.ContainsKey(id))
        {
            return;
        }

        DesignerNode? node = _workspace.Session.Current.Find(id);
        if (node is null)
        {
            return;
        }

        _moveHandles[id] = AddMoveHandle(chrome, node);
        _resizeHandles[id] = AddResizeHandle(chrome, node);
    }

    private void RemoveSelectionHandles(Grid chrome, ElementId id)
    {
        if (_moveHandles.Remove(id, out View? moveHandle))
        {
            chrome.Remove(moveHandle);
        }

        if (_resizeHandles.Remove(id, out View? resizeHandle))
        {
            chrome.Remove(resizeHandle);
        }
    }

    private Grid CreateGridTrackSurface(Grid content)
    {
        var surface = new Grid();
        surface.Add(content);
        AddGridTrackOverlay(surface, content);
        return surface;
    }

    private void AddGridTrackOverlay(Grid surface, Grid content)
    {
        var drawable = new GridTrackOverlayDrawable();
        var overlay = new GraphicsView
        {
            InputTransparent = true,
            Drawable = drawable
        };
        void UpdateTracks()
        {
            drawable.Update(
                content,
                new RectF(0, 0, (float)content.Width, (float)content.Height),
                _viewport.Zoom);
            overlay.Invalidate();
        }

        content.SizeChanged += (_, _) => UpdateTracks();
        overlay.Loaded += (_, _) => UpdateTracks();
        _gridTrackUpdates.Add(UpdateTracks);
        surface.Add(overlay);
    }

    private void AttachContextMenu(View target, ElementId elementId)
    {
#if WINDOWS
        target.HandlerChanged += (_, _) =>
        {
            if (target.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement native ||
                _contextMenuTargets.TryGetValue(native, out _))
            {
                return;
            }

            _contextMenuTargets.Add(native, new object());
            native.ContextRequested += (_, args) =>
            {
                var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
                AddContextMenuItem(menu, "Cut", () =>
                {
                    SelectForContextMenu(elementId);
                    _workspace.CutSelection();
                });
                AddContextMenuItem(menu, "Copy", () =>
                {
                    SelectForContextMenu(elementId);
                    _workspace.CopySelection();
                });
                AddContextMenuItem(menu, "Paste", () =>
                {
                    SelectForContextMenu(elementId);
                    _workspace.Paste();
                });
                AddContextMenuItem(menu, "Duplicate", () =>
                {
                    SelectForContextMenu(elementId);
                    _workspace.DuplicateSelection();
                });
                AddContextMenuItem(menu, "Delete", () =>
                {
                    SelectForContextMenu(elementId);
                    _workspace.DeleteSelection();
                });
                menu.ShowAt(native);
                args.Handled = true;
            };
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

    private View AddMoveHandle(Grid chrome, DesignerNode node)
    {
        var handle = new Border
        {
            AutomationId = $"move-{node.Id.Value}",
            WidthRequest = 28,
            HeightRequest = 14,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = -4,
            TranslationY = -18,
            BackgroundColor = Color.FromArgb("#7C5CFF"),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(5)
            },
            Content = new Label
            {
                InputTransparent = true,
                Text = ":::",
                FontSize = 9,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            }
        };
        RectD start = node.Bounds ?? new RectD(0, 0, 160, 48);
        double totalX = 0;
        double totalY = 0;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Running)
            {
                totalX = args.TotalX;
                totalY = args.TotalY;
                chrome.TranslationX = _viewport.ToDesignDelta(totalX);
                chrome.TranslationY = _viewport.ToDesignDelta(totalY);
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                chrome.TranslationX = 0;
                chrome.TranslationY = 0;
                _workspace.SetBounds(node.Id, start with
                {
                    X = Math.Max(0, _viewport.Snap(
                        start.X + _viewport.ToDesignDelta(totalX))),
                    Y = Math.Max(0, _viewport.Snap(
                        start.Y + _viewport.ToDesignDelta(totalY)))
                });
            }
            else if (args.StatusType == GestureStatus.Canceled)
            {
                chrome.TranslationX = 0;
                chrome.TranslationY = 0;
            }
        };
        handle.GestureRecognizers.Add(pan);
        chrome.Add(handle);
        return handle;
    }

    private View AddResizeHandle(Grid chrome, DesignerNode node)
    {
        var handle = new Border
        {
            AutomationId = $"resize-{node.Id.Value}",
            WidthRequest = 12,
            HeightRequest = 12,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            TranslationX = 6,
            TranslationY = 6,
            BackgroundColor = Color.FromArgb("#7C5CFF"),
            Stroke = Colors.White,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(6)
            }
        };
        RectD start = node.Bounds ?? new RectD(0, 0, 160, 48);
        double totalX = 0;
        double totalY = 0;
        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, args) =>
        {
            if (args.StatusType == GestureStatus.Running)
            {
                totalX = args.TotalX;
                totalY = args.TotalY;
                double width = Math.Max(
                    24,
                    _viewport.Snap(start.Width + _viewport.ToDesignDelta(totalX)));
                double height = Math.Max(
                    24,
                    _viewport.Snap(start.Height + _viewport.ToDesignDelta(totalY)));
                chrome.WidthRequest = width;
                chrome.HeightRequest = height;
            }
            else if (args.StatusType == GestureStatus.Completed)
            {
                double width = Math.Max(
                    24,
                    _viewport.Snap(start.Width + _viewport.ToDesignDelta(totalX)));
                double height = Math.Max(
                    24,
                    _viewport.Snap(start.Height + _viewport.ToDesignDelta(totalY)));
                _workspace.SetBounds(node.Id, start with { Width = width, Height = height });
            }
            else if (args.StatusType == GestureStatus.Canceled)
            {
                chrome.WidthRequest = start.Width;
                chrome.HeightRequest = start.Height;
            }
        };
        handle.GestureRecognizers.Add(pan);
        chrome.Add(handle);
        return handle;
    }

    private static bool TryGetWindowBounds(View view, out RectD bounds)
    {
#if WINDOWS
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native &&
            native.XamlRoot is not null)
        {
            Windows.Foundation.Point origin = native
                .TransformToVisual(null)
                .TransformPoint(new Windows.Foundation.Point());
            Windows.Foundation.Point opposite = native
                .TransformToVisual(null)
                .TransformPoint(new Windows.Foundation.Point(
                    native.ActualWidth,
                    native.ActualHeight));
            bounds = new RectD(
                Math.Min(origin.X, opposite.X),
                Math.Min(origin.Y, opposite.Y),
                Math.Abs(opposite.X - origin.X),
                Math.Abs(opposite.Y - origin.Y));
            return bounds.Width > 0 && bounds.Height > 0;
        }
#endif
        bounds = default;
        return false;
    }

    private void UpdateOutline(Border outline, ElementId id)
    {
        bool selected = _workspace.SelectedIds.Contains(id);
        bool dropTarget = id == _workspace.DropTargetId;
        outline.Stroke = dropTarget
            ? Color.FromArgb("#38BDF8")
            : selected
                ? Color.FromArgb("#7C5CFF")
                : Colors.Transparent;
        outline.StrokeThickness = dropTarget ? 3 : selected ? 2 : 0;
    }

    private void SelectForContextMenu(ElementId elementId)
    {
        if (!_workspace.SelectedIds.Contains(elementId))
        {
            _workspace.Select(elementId);
        }
    }

#if WINDOWS
    private static bool IsControlPressed() =>
        Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
#endif

    private void RemoveActiveDropPreview()
    {
        if (_activeDropPreview?.Parent is Layout layout)
        {
            layout.Children.Remove(_activeDropPreview);
        }

        _activeDropPreview = null;
    }

    private void ApplyProperties(
        View view,
        ControlDescriptor descriptor,
        DesignerNode node)
    {
        foreach ((string name, DesignerValue designerValue) in node.Properties)
        {
            _ = TryApplyPropertyValue(view, descriptor, name, designerValue);
        }

        GetWritableProperties(descriptor).TryGetValue("Text", out PropertyInfo? textProperty);
        if (!node.Properties.ContainsKey("Text") && textProperty?.CanWrite == true && textProperty.PropertyType == typeof(string))
        {
            textProperty.SetValue(view, descriptor.DisplayName);
        }

        ApplyPreviewTextContrast(view, descriptor, node);
    }

    private static bool TryApplyPropertyValue(
        View view,
        ControlDescriptor descriptor,
        string propertyName,
        DesignerValue designerValue)
    {
        string text;
        if (designerValue.Kind == DesignerValueKind.Literal)
        {
            text = designerValue.Text;
        }
        else if (designerValue.Kind == DesignerValueKind.MarkupExtension &&
                 DesignerMarkupPreview.TryGetLiteral(designerValue.Text, out string preview))
        {
            text = preview;
        }
        else
        {
            return false;
        }

        if (!GetWritableProperties(descriptor).TryGetValue(propertyName, out PropertyInfo? property) ||
            !DesignerValueConverter.TryConvert(text, property.PropertyType, out object? value))
        {
            return false;
        }

        property.SetValue(view, value);
        return true;
    }

    private static IReadOnlyDictionary<string, PropertyInfo> GetWritableProperties(
        ControlDescriptor descriptor) =>
        RuntimePropertyCache.GetWritableProperties(descriptor.RuntimeType);

    private void ApplyPreviewTextContrast(
        View view,
        ControlDescriptor descriptor,
        DesignerNode node)
    {
        if (node.Properties.ContainsKey("TextColor") ||
            view.BackgroundColor is Color background && background.Alpha > 0.05f)
        {
            return;
        }

        PropertyDescriptor? textColorDescriptor = descriptor.Properties
            .FirstOrDefault(property =>
                property.Name == "TextColor" &&
                !property.IsReadOnly &&
                property.ValueType == typeof(Color));
        PropertyInfo? textColorProperty = null;
        if (textColorDescriptor is not null)
        {
            GetWritableProperties(descriptor).TryGetValue(
                textColorDescriptor.Name,
                out textColorProperty);
        }
        textColorProperty?.SetValue(
            view,
            Colors.Black);
    }

    private static void EnsureDesignSize(View view, ControlDescriptor descriptor)
    {
        if (!descriptor.AcceptsChildren)
        {
            return;
        }

        view.MinimumWidthRequest = Math.Max(120, view.MinimumWidthRequest);
        view.MinimumHeightRequest = Math.Max(80, view.MinimumHeightRequest);
    }

    private static View CreateUnknownControl(DesignerNode node) =>
        new Border
        {
            Padding = 12,
            BackgroundColor = Color.FromArgb("#FFF3CD"),
            Stroke = Color.FromArgb("#D39E00"),
            Content = new Label
            {
                Text = $"Unavailable: {node.ControlType.XamlName}",
                TextColor = Color.FromArgb("#664D03")
            }
        };

    private static View CreateUnavailableControl(DesignerNode node, string reason) =>
        new Border
        {
            Padding = 12,
            BackgroundColor = Color.FromArgb("#FDECEC"),
            Stroke = Color.FromArgb("#DC2626"),
            Content = new Label
            {
                Text = $"Could not render {node.ControlType.XamlName}: {reason}",
                TextColor = Color.FromArgb("#7F1D1D")
            }
        };

    private static bool IsRecoverableMaterializationFailure(Exception exception) =>
        exception is not OutOfMemoryException and
        not StackOverflowException and
        not AccessViolationException and
        not AppDomainUnloadedException and
        not BadImageFormatException;

    private static void SetVisualProperty(View parent, string propertyName, View child)
    {
        PropertyInfo property = VisualContentProperty.FindAll(parent.GetType())
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, propertyName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Visual property '{propertyName}' is unavailable on '{parent.GetType().FullName}'.");
        property.SetValue(parent, child);
    }

    private sealed record ManualDropTarget(
        ElementId Id,
        View View,
        ILayoutAdapter Adapter,
        RectD Bounds);

    private sealed record ManualDragState(
        PointD Start,
        IReadOnlyList<ManualDropTarget> Targets);
}
