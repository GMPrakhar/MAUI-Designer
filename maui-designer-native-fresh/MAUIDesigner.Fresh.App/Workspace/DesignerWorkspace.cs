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

    public ElementId Add(ControlDescriptor descriptor, ElementId? requestedParentId = null, PointD? position = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ElementId parentId = requestedParentId ?? ResolveInsertionParent();
        EnsureValidParent(parentId);
        var id = new ElementId($"{descriptor.Id.XamlName.ToLowerInvariant()}-{Interlocked.Increment(ref _nextId)}");
        RectD? bounds = CreateInitialBounds(descriptor, parentId, position);
        Session.Execute(new AddElementCommand(parentId, new DesignerNode(id, descriptor.Id, bounds: bounds)));
        Select(id);
        return id;
    }

    public void Reparent(ElementId elementId, ElementId parentId, PointD? position = null)
    {
        EnsureValidParent(parentId);
        DesignerNode node = Session.Current.Find(elementId)
            ?? throw new KeyNotFoundException($"Element '{elementId}' was not found.");
        RectD? bounds = node.Bounds;
        if (IsAbsoluteLayout(parentId))
        {
            double x = position?.X ?? bounds?.X ?? 24;
            double y = position?.Y ?? bounds?.Y ?? 24;
            bounds = new RectD(x, y, bounds?.Width ?? 160, bounds?.Height ?? 48);
        }
        else
        {
            bounds = null;
        }

        Session.Execute(new ReparentElementCommand(elementId, parentId, Bounds: bounds));
        Select(elementId);
        ClearDropTarget();
    }

    public void SetBounds(ElementId elementId, RectD bounds) =>
        Session.Execute(new SetBoundsCommand(elementId, bounds));

    public void SetDropTarget(ElementId? id)
    {
        if (DropTargetId == id)
        {
            return;
        }

        DropTargetId = id;
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
        return _catalog.TryGet(selected.ControlType, out ControlDescriptor? descriptor) &&
            descriptor?.AcceptsChildren == true
                ? selected.Id
                : FindParent(Session.Current.Root, selected.Id)?.Id ?? Session.Current.Root.Id;
    }

    private void EnsureValidParent(ElementId parentId)
    {
        DesignerNode parent = Session.Current.Find(parentId)
            ?? throw new KeyNotFoundException($"Element '{parentId}' was not found.");
        if (!_catalog.TryGet(parent.ControlType, out ControlDescriptor? descriptor) ||
            descriptor?.AcceptsChildren != true)
        {
            throw new InvalidOperationException($"'{parent.ControlType.XamlName}' cannot contain child controls.");
        }
    }

    private RectD? CreateInitialBounds(
        ControlDescriptor descriptor,
        ElementId parentId,
        PointD? position)
    {
        if (!IsAbsoluteLayout(parentId))
        {
            return null;
        }

        double offset = 24 + ((_nextId - 1) % 8) * 14;
        return new RectD(
            position?.X ?? offset,
            position?.Y ?? offset,
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
