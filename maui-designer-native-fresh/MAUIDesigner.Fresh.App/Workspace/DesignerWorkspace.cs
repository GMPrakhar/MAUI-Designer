using System.Collections.Immutable;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Workspace;

public sealed class DesignerWorkspace
{
    private readonly IControlCatalog _catalog;
    private long _nextId;

    public DesignerWorkspace(IControlCatalog catalog)
    {
        _catalog = catalog;
        ControlDescriptor root = catalog.Controls.First(descriptor => descriptor.RuntimeType == typeof(AbsoluteLayout));
        Session = new DocumentSession(DesignerDocument.Create(root.Id));
        SelectedId = Session.Current.Root.Id;
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? InteractionChanged;

    public DocumentSession Session { get; }

    public ElementId SelectedId { get; private set; }

    public ElementId? DropTargetId { get; private set; }

    public LayoutPlacement? DropPlacement { get; private set; }

    public void Select(ElementId id)
    {
        if (Session.Current.Find(id) is null)
        {
            throw new KeyNotFoundException($"Element '{id}' was not found.");
        }

        if (SelectedId == id)
        {
            return;
        }

        SelectedId = id;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public ElementId Add(
        ControlDescriptor descriptor,
        ElementId? requestedParentId = null,
        LayoutPlacement? placement = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ElementId parentId = requestedParentId ?? ResolveInsertionParent();
        EnsureValidParent(parentId);
        var id = new ElementId($"{descriptor.Id.XamlName.ToLowerInvariant()}-{Interlocked.Increment(ref _nextId)}");
        RectD? bounds = CreateInitialBounds(descriptor, parentId, placement?.Bounds);
        ImmutableDictionary<string, DesignerValue> properties = placement?.PropertyUpdates?
            .Where(update => update.Value is not null)
            .ToImmutableDictionary(update => update.Key, update => update.Value!, StringComparer.Ordinal)
            ?? ImmutableDictionary<string, DesignerValue>.Empty;
        Session.Execute(new AddElementCommand(
            parentId,
            new DesignerNode(id, descriptor.Id, properties, bounds: bounds),
            placement?.DestinationIndex ?? -1));
        Select(id);
        return id;
    }

    public void Reparent(
        ElementId elementId,
        ElementId parentId,
        LayoutPlacement? placement = null)
    {
        EnsureValidParent(parentId, elementId);
        DesignerNode node = Session.Current.Find(elementId)
            ?? throw new KeyNotFoundException($"Element '{elementId}' was not found.");
        RectD? bounds = placement?.Bounds;
        if (bounds is not null && node.Bounds is RectD existing)
        {
            bounds = bounds.Value with { Width = existing.Width, Height = existing.Height };
        }
        else if (bounds is not null &&
                 _catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor))
        {
            bounds = bounds.Value with
            {
                Width = descriptor!.AcceptsChildren ? 280 : 160,
                Height = descriptor.AcceptsChildren ? 180 : 48
            };
        }

        Session.Execute(new PlaceElementCommand(
            elementId,
            parentId,
            placement?.DestinationIndex ?? -1,
            bounds,
            placement?.PropertyUpdates));
        Select(elementId);
        ClearDropTarget();
    }

    public void SetBounds(ElementId elementId, RectD bounds) =>
        Session.Execute(new SetBoundsCommand(elementId, bounds));

    public void SetDropTarget(ElementId? id, LayoutPlacement? placement = null)
    {
        if (DropTargetId == id && DropPlacement == placement)
        {
            return;
        }

        DropTargetId = id;
        DropPlacement = placement;
        InteractionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearDropTarget() => SetDropTarget(null);

    public void DeleteSelection()
    {
        if (SelectedId == Session.Current.Root.Id)
        {
            return;
        }

        Session.Execute(new RemoveElementCommand(SelectedId));
        Select(Session.Current.Root.Id);
    }

    public void ReplaceDocument(DesignerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Session.Execute(new ReplaceDocumentCommand(document));
        Select(document.Root.Id);
        ClearDropTarget();
    }

    private ElementId ResolveInsertionParent()
    {
        DesignerNode selected = Session.Current.Find(SelectedId) ?? Session.Current.Root;
        return CanAcceptChild(selected.Id)
                ? selected.Id
                : FindParent(Session.Current.Root, selected.Id)?.Id ?? Session.Current.Root.Id;
    }

    public bool CanAcceptChild(ElementId parentId, ElementId? movingId = null)
    {
        DesignerNode? parent = Session.Current.Find(parentId);
        if (parent is null ||
            !_catalog.TryGet(parent.ControlType, out ControlDescriptor? descriptor) ||
            descriptor?.AcceptsChildren != true)
        {
            return false;
        }

        if (typeof(Layout).IsAssignableFrom(descriptor.RuntimeType))
        {
            return true;
        }

        return parent.Children.Length == 0 ||
            movingId is not null &&
            parent.Children.Any(child => child.Id == movingId.Value);
    }

    private void EnsureValidParent(ElementId parentId, ElementId? movingId = null)
    {
        DesignerNode parent = Session.Current.Find(parentId)
            ?? throw new KeyNotFoundException($"Element '{parentId}' was not found.");
        if (!CanAcceptChild(parentId, movingId))
        {
            throw new InvalidOperationException(
                $"'{parent.ControlType.XamlName}' cannot accept another child control.");
        }
    }

    private RectD? CreateInitialBounds(
        ControlDescriptor descriptor,
        ElementId parentId,
        RectD? requestedBounds)
    {
        if (!IsAbsoluteLayout(parentId))
        {
            return null;
        }

        double offset = 24 + ((_nextId - 1) % 8) * 14;
        return new RectD(
            requestedBounds?.X ?? offset,
            requestedBounds?.Y ?? offset,
            descriptor.AcceptsChildren ? 280 : 160,
            descriptor.AcceptsChildren ? 180 : 48);
    }

    private bool IsAbsoluteLayout(ElementId id)
    {
        DesignerNode? node = Session.Current.Find(id);
        return node is not null &&
            _catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) &&
            descriptor?.RuntimeType == typeof(AbsoluteLayout);
    }

    private static DesignerNode? FindParent(DesignerNode parent, ElementId childId)
    {
        if (parent.Children.Any(child => child.Id == childId))
        {
            return parent;
        }

        foreach (DesignerNode child in parent.Children)
        {
            DesignerNode? match = FindParent(child, childId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
