using System.Collections.Immutable;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.App.Workspace;

public sealed class DesignerWorkspace
{
    private const double PasteOffset = 16;
    private readonly IControlCatalog _catalog;
    private readonly ControlTypeId _defaultRootType;
    private long _nextId;
    private ImmutableArray<DesignerNode> _clipboard = [];
    private ImmutableHashSet<ElementId> _selectedIds = [];
    private int _pasteCount;

    public DesignerWorkspace(IControlCatalog catalog)
    {
        _catalog = catalog;
        ControlDescriptor root = catalog.Controls.First(descriptor => descriptor.RuntimeType == typeof(AbsoluteLayout));
        _defaultRootType = root.Id;
        Session = new DocumentSession(DesignerDocument.Create(root.Id));
        SelectedId = Session.Current.Root.Id;
        _selectedIds = ImmutableHashSet.Create(SelectedId);
    }

    public event EventHandler? SelectionChanged;

    public event EventHandler? InteractionChanged;

    public DocumentSession Session { get; }

    public ElementId SelectedId { get; private set; }

    public IReadOnlySet<ElementId> SelectedIds => _selectedIds;

    public int SelectionCount => _selectedIds.Count;

    public ElementId? DropTargetId { get; private set; }

    public LayoutPlacement? DropPlacement { get; private set; }

    public bool CanCopy => GetSelectionRoots().Count > 0;

    public bool CanCut => CanCopy;

    public bool CanPaste => !_clipboard.IsDefaultOrEmpty &&
        TryResolveInsertionParent(_clipboard.Length, out _);

    public bool CanDuplicate => GetSelectionRoots().Count > 0 &&
        GetSelectionRoots().All(node =>
            node.Id != Session.Current.Root.Id &&
            TryGetSiblingPosition(node.Id, out DesignerNode? parent, out _) &&
            CanAcceptChild(parent!.Id));

    public bool CanMoveSelectionUp =>
        _selectedIds.Count == 1 &&
        TryGetSelectedSiblingPosition(out _, out int index) && index > 0;

    public bool CanMoveSelectionDown =>
        _selectedIds.Count == 1 &&
        TryGetSelectedSiblingPosition(out DesignerNode? parent, out int index) &&
        index < parent!.Children.Length - 1;

    public void Select(ElementId id)
    {
        if (Session.Current.Find(id) is null)
        {
            throw new KeyNotFoundException($"Element '{id}' was not found.");
        }

        SetSelection([id], additive: false);
    }

    public void ToggleSelection(ElementId id)
    {
        if (Session.Current.Find(id) is null)
        {
            throw new KeyNotFoundException($"Element '{id}' was not found.");
        }

        if (id == Session.Current.Root.Id)
        {
            Select(id);
            return;
        }

        if (_selectedIds.Contains(id))
        {
            if (_selectedIds.Count == 1)
            {
                Select(Session.Current.Root.Id);
                return;
            }

            ImmutableHashSet<ElementId> remaining = _selectedIds.Remove(id);
            ElementId primary = SelectedId == id
                ? GetNodesInDocumentOrder().Last(node => remaining.Contains(node.Id)).Id
                : SelectedId;
            ApplySelection(remaining, primary);
            return;
        }

        ApplySelection(_selectedIds.Remove(Session.Current.Root.Id).Add(id), id);
    }

    public void SelectAll()
    {
        ElementId[] descendants = GetNodesInDocumentOrder()
            .Skip(1)
            .Select(node => node.Id)
            .ToArray();
        SetSelection(
            descendants.Length == 0 ? [Session.Current.Root.Id] : descendants,
            additive: false);
    }

    public void SetSelection(IEnumerable<ElementId> ids, bool additive)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ElementId[] requested = ids
            .Where(id => Session.Current.Find(id) is not null)
            .Distinct()
            .ToArray();
        ImmutableHashSet<ElementId> selection = additive
            ? _selectedIds.Union(requested)
            : requested.ToImmutableHashSet();
        if (selection.Count > 1)
        {
            selection = selection.Remove(Session.Current.Root.Id);
        }

        if (selection.Count == 0)
        {
            selection = ImmutableHashSet.Create(Session.Current.Root.Id);
        }

        ElementId primary = requested.LastOrDefault(id => selection.Contains(id));
        if (primary == default || !selection.Contains(primary))
        {
            primary = GetNodesInDocumentOrder().Last(node => selection.Contains(node.Id)).Id;
        }

        ApplySelection(selection, primary);
    }

    private void ApplySelection(
        ImmutableHashSet<ElementId> selection,
        ElementId primary)
    {
        if (_selectedIds.SetEquals(selection) && SelectedId == primary)
        {
            return;
        }

        _selectedIds = selection;
        SelectedId = primary;
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
        IReadOnlyList<DesignerNode> selected = GetSelectionRoots();
        if (selected.Count == 0)
        {
            return;
        }

        if (selected.Any(node => node.Id == Session.Current.Root.Id))
        {
            Session.Execute(new ReplaceDocumentCommand(
                DesignerDocument.Create(_defaultRootType),
                "Clear document"));
            Select(Session.Current.Root.Id);
            return;
        }

        Session.Execute(new CompositeDocumentCommand(
            selected
                .Select(node => (IDocumentCommand)new RemoveElementCommand(node.Id))
                .ToArray(),
            selected.Count == 1 ? "Delete element" : $"Delete {selected.Count} elements"));
        Select(Session.Current.Root.Id);
    }

    public void CopySelection()
    {
        if (!CanCopy)
        {
            return;
        }

        _clipboard = GetSelectionRoots().ToImmutableArray();
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
        if (_clipboard.IsDefaultOrEmpty ||
            !TryResolveInsertionParent(_clipboard.Length, out ElementId parentId))
        {
            return null;
        }

        _pasteCount++;
        DesignerNode[] clones = _clipboard
            .Select(source =>
                PrepareCloneForParent(source, parentId, _pasteCount))
            .ToArray();
        Session.Execute(new CompositeDocumentCommand(
            clones
                .Select(clone => (IDocumentCommand)new AddElementCommand(parentId, clone))
                .ToArray(),
            clones.Length == 1 ? "Paste element" : $"Paste {clones.Length} elements"));
        SetSelection(clones.Select(clone => clone.Id), additive: false);
        return clones[^1].Id;
    }

    public ElementId? DuplicateSelection()
    {
        IReadOnlyList<DesignerNode> selected = GetSelectionRoots()
            .Where(node => node.Id != Session.Current.Root.Id)
            .ToArray();
        if (selected.Count == 0 || !CanDuplicate)
        {
            return null;
        }

        var commands = new List<IDocumentCommand>();
        var clones = new List<DesignerNode>();
        foreach (IGrouping<ElementId, DesignerNode> group in selected.GroupBy(node =>
                 FindParent(Session.Current.Root, node.Id)!.Id))
        {
            DesignerNode parent = Session.Current.Find(group.Key)!;
            int inserted = 0;
            foreach (DesignerNode source in group.OrderBy(node =>
                         IndexOf(parent.Children, node.Id)))
            {
                DesignerNode clone = PrepareCloneForParent(source, parent.Id, 1);
                commands.Add(new AddElementCommand(
                    parent.Id,
                    clone,
                    IndexOf(parent.Children, source.Id) + 1 + inserted));
                clones.Add(clone);
                inserted++;
            }
        }

        Session.Execute(new CompositeDocumentCommand(
            commands,
            clones.Count == 1 ? "Duplicate element" : $"Duplicate {clones.Count} elements"));
        SetSelection(clones.Select(clone => clone.Id), additive: false);
        return clones[^1].Id;
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
        if (TryResolveInsertionParent(1, out ElementId parentId))
        {
            return parentId;
        }

        throw new InvalidOperationException("The document has no container that can accept another child control.");
    }

    private bool TryResolveInsertionParent(int childCount, out ElementId parentId)
    {
        DesignerNode? candidate = Session.Current.Find(SelectedId) ?? Session.Current.Root;
        while (candidate is not null)
        {
            if (CanAcceptChildren(candidate.Id, childCount))
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

    private bool CanAcceptChildren(ElementId parentId, int childCount)
    {
        if (!CanAcceptChild(parentId))
        {
            return false;
        }

        DesignerNode parent = Session.Current.Find(parentId)!;
        ControlDescriptor descriptor = _catalog.Controls.First(control =>
            control.Id == parent.ControlType);
        return typeof(Layout).IsAssignableFrom(descriptor.RuntimeType) ||
            parent.Children.Length + childCount <= 1;
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
        => TryGetSiblingPosition(SelectedId, out parent, out index);

    private bool TryGetSiblingPosition(
        ElementId id,
        out DesignerNode? parent,
        out int index)
    {
        parent = FindParent(Session.Current.Root, id);
        index = -1;
        if (parent is not null)
        {
            for (int childIndex = 0; childIndex < parent.Children.Length; childIndex++)
            {
                if (parent.Children[childIndex].Id == id)
                {
                    index = childIndex;
                    break;
                }
            }
        }

        return parent is not null && index >= 0;
    }

    private IReadOnlyList<DesignerNode> GetSelectionRoots()
    {
        DesignerNode[] selected = GetNodesInDocumentOrder()
            .Where(node => _selectedIds.Contains(node.Id))
            .ToArray();
        return selected
            .Where(node => !selected.Any(candidate =>
                candidate.Id != node.Id && candidate.Find(node.Id) is not null))
            .ToArray();
    }

    private IEnumerable<DesignerNode> GetNodesInDocumentOrder() =>
        Enumerate(Session.Current.Root);

    private static IEnumerable<DesignerNode> Enumerate(DesignerNode node)
    {
        yield return node;
        foreach (DesignerNode child in node.Children)
        {
            foreach (DesignerNode descendant in Enumerate(child))
            {
                yield return descendant;
            }
        }
    }

    private static int IndexOf(
        ImmutableArray<DesignerNode> children,
        ElementId id)
    {
        for (int index = 0; index < children.Length; index++)
        {
            if (children[index].Id == id)
            {
                return index;
            }
        }

        return -1;
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
