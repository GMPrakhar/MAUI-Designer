using System.Collections.Immutable;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Workspace;

public sealed class DesignerWorkspace
{
    private const double PasteOffset = 16;
    private readonly IControlCatalog _catalog;
    private long _nextId;
    private DesignerNode? _clipboard;
    private int _pasteCount;

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

    public bool CanCopy => SelectedId != Session.Current.Root.Id &&
        Session.Current.Find(SelectedId) is not null;

    public bool CanCut => SelectedId != Session.Current.Root.Id &&
        Session.Current.Find(SelectedId) is not null;

    public bool CanPaste => _clipboard is not null && TryResolveInsertionParent(out _);

    public bool CanDuplicate => TryGetSelectedSiblingPosition(out DesignerNode? parent, out _) &&
        CanAcceptChild(parent!.Id);

    public bool CanMoveSelectionUp =>
        TryGetSelectedSiblingPosition(out _, out int index) && index > 0;

    public bool CanMoveSelectionDown =>
        TryGetSelectedSiblingPosition(out DesignerNode? parent, out int index) &&
        index < parent!.Children.Length - 1;

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

        ElementId removedId = SelectedId;
        DesignerNode parent = FindParent(Session.Current.Root, removedId)
            ?? throw new InvalidOperationException($"Element '{removedId}' has no parent.");
        Session.Execute(new RemoveElementCommand(removedId));
        Select(parent.Id);
    }

    public void CopySelection()
    {
        if (!CanCopy)
        {
            return;
        }

        _clipboard = Session.Current.Find(SelectedId)
            ?? throw new KeyNotFoundException($"Element '{SelectedId}' was not found.");
        _pasteCount = 0;
        InteractionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CutSelection()
    {
        if (!CanCut)
        {
            return;
        }

        CopySelection();
        DeleteSelection();
    }

    public ElementId? Paste()
    {
        if (_clipboard is null || !TryResolveInsertionParent(out ElementId parentId))
        {
            return null;
        }

        _pasteCount++;
        DesignerNode clone = PrepareCloneForParent(_clipboard, parentId, _pasteCount);
        Session.Execute(new AddElementCommand(parentId, clone));
        Select(clone.Id);
        return clone.Id;
    }

    public ElementId? DuplicateSelection()
    {
        if (!TryGetSelectedSiblingPosition(out DesignerNode? parent, out int index) ||
            !CanAcceptChild(parent!.Id))
        {
            return null;
        }

        DesignerNode source = Session.Current.Find(SelectedId)!;
        DesignerNode clone = PrepareCloneForParent(source, parent.Id, 1);
        Session.Execute(new AddElementCommand(parent.Id, clone, index + 1));
        Select(clone.Id);
        return clone.Id;
    }

    public bool MoveSelectionUp() => MoveSelection(-1);

    public bool MoveSelectionDown() => MoveSelection(1);

    public bool MoveSelection(int direction)
    {
        if (direction is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be -1 or 1.");
        }

        if (!TryGetSelectedSiblingPosition(out DesignerNode? parent, out int index))
        {
            return false;
        }

        int destinationIndex = index + direction;
        if (destinationIndex < 0 || destinationIndex >= parent!.Children.Length)
        {
            return false;
        }

        Session.Execute(new ReorderElementCommand(SelectedId, destinationIndex));
        return true;
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
        if (TryResolveInsertionParent(out ElementId parentId))
        {
            return parentId;
        }

        throw new InvalidOperationException("The document has no container that can accept another child control.");
    }

    private bool TryResolveInsertionParent(out ElementId parentId)
    {
        DesignerNode? candidate = Session.Current.Find(SelectedId) ?? Session.Current.Root;
        while (candidate is not null)
        {
            if (CanAcceptChild(candidate.Id))
            {
                parentId = candidate.Id;
                return true;
            }

            candidate = FindParent(Session.Current.Root, candidate.Id);
        }

        parentId = default;
        return false;
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

        if (movingId is ElementId id &&
            Session.Current.Find(id)?.Find(parentId) is not null)
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

    private DesignerNode PrepareCloneForParent(DesignerNode source, ElementId parentId, int offsetMultiplier)
    {
        DesignerNode clone = DesignerNodeCloner.CloneSubtree(source);
        RectD? bounds = null;
        if (IsAbsoluteLayout(parentId))
        {
            RectD sourceBounds = source.Bounds ?? CreateDefaultBounds(source);
            double offset = PasteOffset * offsetMultiplier;
            bounds = sourceBounds with
            {
                X = sourceBounds.X + offset,
                Y = sourceBounds.Y + offset
            };
        }

        return clone with
        {
            ParentPropertyName = null,
            Bounds = bounds,
            Properties = clone.Properties
                .Remove("AbsoluteLayout.LayoutBounds")
                .Remove("AbsoluteLayout.LayoutFlags")
        };
    }

    private RectD CreateDefaultBounds(DesignerNode node)
    {
        bool acceptsChildren = _catalog.TryGet(node.ControlType, out ControlDescriptor? descriptor) &&
            descriptor?.AcceptsChildren == true;
        return new RectD(24, 24, acceptsChildren ? 280 : 160, acceptsChildren ? 180 : 48);
    }

    private bool TryGetSelectedSiblingPosition(out DesignerNode? parent, out int index)
    {
        parent = FindParent(Session.Current.Root, SelectedId);
        index = -1;
        if (parent is not null)
        {
            for (int childIndex = 0; childIndex < parent.Children.Length; childIndex++)
            {
                if (parent.Children[childIndex].Id == SelectedId)
                {
                    index = childIndex;
                    break;
                }
            }
        }

        return parent is not null && index >= 0;
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
