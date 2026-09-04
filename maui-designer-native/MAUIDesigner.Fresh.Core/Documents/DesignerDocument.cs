using System.Collections.Immutable;

namespace MAUIDesigner.Fresh.Core.Documents;

public sealed record DesignerDocument(
    DesignerNode Root,
    ImmutableDictionary<string, string> Namespaces,
    XamlDocumentMetadata? XamlMetadata = null)
{
    public static DesignerDocument Create(ControlTypeId rootType) =>
        new(
            new DesignerNode(new ElementId("root"), rootType),
            ImmutableDictionary<string, string>.Empty.Add(string.Empty, rootType.XamlNamespace));

    public DesignerNode? Find(ElementId id) => Root.Find(id);

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Root);
        var ids = new HashSet<ElementId>();
        ValidateNode(Root, ids);
    }

    private static void ValidateNode(DesignerNode node, HashSet<ElementId> ids)
    {
        if (!ids.Add(node.Id))
        {
            throw new InvalidOperationException($"Duplicate element id '{node.Id}'.");
        }

        var visualProperties = new HashSet<string>(StringComparer.Ordinal);
        foreach (DesignerNode child in node.Children)
        {
            if (child.ParentPropertyName is string propertyName &&
                !visualProperties.Add(propertyName))
            {
                throw new InvalidOperationException(
                    $"Visual property '{propertyName}' on '{node.Id}' has multiple children.");
            }

            ValidateNode(child, ids);
        }
    }
}
