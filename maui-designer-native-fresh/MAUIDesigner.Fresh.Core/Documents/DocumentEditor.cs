using System.Collections.Immutable;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.Core.Documents;

internal static class DocumentEditor
{
    public static DesignerDocument Add(
        DesignerDocument document,
        ElementId parentId,
        DesignerNode node,
        int index)
    {
        ArgumentNullException.ThrowIfNull(node);
        EnsureUniqueSubtree(document, node);

        DesignerNode root = Rewrite(document.Root, parentId, parent =>
            parent with { Children = Insert(parent.Children, node, index) });
        return document with { Root = root };
    }

    public static DesignerDocument Remove(DesignerDocument document, ElementId id)
    {
        if (document.Root.Id == id)
        {
            throw new InvalidOperationException("The document root cannot be removed.");
        }

        bool removed = false;
        DesignerNode root = RemoveCore(document.Root, id, ref removed);
        if (!removed)
        {
            throw Missing(id);
        }

        return document with { Root = root };
    }

    public static DesignerDocument Reparent(
        DesignerDocument document,
        ElementId id,
        ElementId destinationParentId,
        int destinationIndex,
        RectD? bounds)
    {
        if (document.Root.Id == id)
        {
            throw new InvalidOperationException("The document root cannot be reparented.");
        }

        DesignerNode node = document.Find(id) ?? throw Missing(id);
        DesignerNode destination = document.Find(destinationParentId) ?? throw Missing(destinationParentId);
        if (node.Id == destination.Id || node.Find(destination.Id) is not null)
        {
            throw new InvalidOperationException("An element cannot be parented to itself or its descendants.");
        }

        DesignerDocument withoutNode = Remove(document, id);
        return Add(withoutNode, destinationParentId, node with { Bounds = bounds }, destinationIndex);
    }

    public static DesignerDocument SetProperty(
        DesignerDocument document,
        ElementId id,
        string propertyName,
        DesignerValue? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        DesignerNode root = Rewrite(document.Root, id, node =>
        {
            ImmutableDictionary<string, DesignerValue> properties = value is null
                ? node.Properties.Remove(propertyName)
                : node.Properties.SetItem(propertyName, value);
            return node with { Properties = properties };
        });
        return document with { Root = root };
    }

    public static DesignerDocument SetBounds(DesignerDocument document, ElementId id, RectD? bounds) =>
        document with { Root = Rewrite(document.Root, id, node => node with { Bounds = bounds }) };

    private static DesignerNode Rewrite(
        DesignerNode current,
        ElementId id,
        Func<DesignerNode, DesignerNode> transform)
    {
        if (current.Id == id)
        {
            return transform(current);
        }

        for (int index = 0; index < current.Children.Length; index++)
        {
            DesignerNode child = current.Children[index];
            DesignerNode rewritten = RewriteOrSame(child, id, transform, out bool found);
            if (found)
            {
                return current with { Children = current.Children.SetItem(index, rewritten) };
            }
        }

        throw Missing(id);
    }

    private static DesignerNode RewriteOrSame(
        DesignerNode current,
        ElementId id,
        Func<DesignerNode, DesignerNode> transform,
        out bool found)
    {
        if (current.Id == id)
        {
            found = true;
            return transform(current);
        }

        for (int index = 0; index < current.Children.Length; index++)
        {
            DesignerNode rewritten = RewriteOrSame(current.Children[index], id, transform, out found);
            if (found)
            {
                return current with { Children = current.Children.SetItem(index, rewritten) };
            }
        }

        found = false;
        return current;
    }

    private static DesignerNode RemoveCore(DesignerNode parent, ElementId id, ref bool removed)
    {
        int directIndex = -1;
        for (int index = 0; index < parent.Children.Length; index++)
        {
            if (parent.Children[index].Id == id)
            {
                directIndex = index;
                break;
            }
        }
        if (directIndex >= 0)
        {
            removed = true;
            return parent with { Children = parent.Children.RemoveAt(directIndex) };
        }

        for (int index = 0; index < parent.Children.Length; index++)
        {
            DesignerNode rewritten = RemoveCore(parent.Children[index], id, ref removed);
            if (removed)
            {
                return parent with { Children = parent.Children.SetItem(index, rewritten) };
            }
        }

        return parent;
    }

    private static ImmutableArray<DesignerNode> Insert(
        ImmutableArray<DesignerNode> children,
        DesignerNode node,
        int index)
    {
        int clampedIndex = index < 0 ? children.Length : Math.Clamp(index, 0, children.Length);
        return children.Insert(clampedIndex, node);
    }

    private static void EnsureUniqueSubtree(DesignerDocument document, DesignerNode subtree)
    {
        var ids = new HashSet<ElementId>();
        CollectIds(subtree, ids);
        foreach (ElementId id in ids)
        {
            if (document.Find(id) is not null)
            {
                throw new InvalidOperationException($"Element id '{id}' already exists.");
            }
        }
    }

    private static void CollectIds(DesignerNode node, HashSet<ElementId> ids)
    {
        if (!ids.Add(node.Id))
        {
            throw new InvalidOperationException($"Duplicate element id '{node.Id}' in inserted subtree.");
        }

        foreach (DesignerNode child in node.Children)
        {
            CollectIds(child, ids);
        }
    }

    private static KeyNotFoundException Missing(ElementId id) =>
        new($"Element '{id}' was not found.");
}
