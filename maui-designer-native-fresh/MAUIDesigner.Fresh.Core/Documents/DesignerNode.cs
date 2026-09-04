using System.Collections.Immutable;
using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.Core.Documents;

public sealed record DesignerNode
{
    public DesignerNode(
        ElementId id,
        ControlTypeId controlType,
        ImmutableDictionary<string, DesignerValue>? properties = null,
        ImmutableArray<DesignerNode> children = default,
        RectD? bounds = null,
        ImmutableArray<XamlSyntaxFragment> preservedContent = default,
        string? parentPropertyName = null)
    {
        Id = id;
        ControlType = controlType ?? throw new ArgumentNullException(nameof(controlType));
        Properties = properties ?? ImmutableDictionary<string, DesignerValue>.Empty;
        Children = children.IsDefault ? [] : children;
        Bounds = bounds;
        PreservedContent = preservedContent.IsDefault ? [] : preservedContent;
        ParentPropertyName = parentPropertyName;
    }

    public ElementId Id { get; init; }

    public ControlTypeId ControlType { get; init; }

    public ImmutableDictionary<string, DesignerValue> Properties { get; init; }

    public ImmutableArray<DesignerNode> Children { get; init; }

    public RectD? Bounds { get; init; }

    public ImmutableArray<XamlSyntaxFragment> PreservedContent { get; init; }

    public string? ParentPropertyName { get; init; }

    public DesignerNode? Find(ElementId id)
    {
        if (Id == id)
        {
            return this;
        }

        foreach (DesignerNode child in Children)
        {
            DesignerNode? match = child.Find(id);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
