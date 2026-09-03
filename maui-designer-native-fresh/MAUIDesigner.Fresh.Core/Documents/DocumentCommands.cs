using MAUIDesigner.Fresh.Core.Geometry;

namespace MAUIDesigner.Fresh.Core.Documents;

public interface IDocumentCommand
{
    string Description { get; }

    DesignerDocument Apply(DesignerDocument document);
}

public sealed record ReplaceDocumentCommand(
    DesignerDocument Replacement,
    string Description = "Apply XAML") : IDocumentCommand
{
    public DesignerDocument Apply(DesignerDocument document)
    {
        ArgumentNullException.ThrowIfNull(Replacement);
        Replacement.Validate();
        return Replacement;
    }
}

public sealed record AddElementCommand(ElementId ParentId, DesignerNode Node, int Index = -1) : IDocumentCommand
{
    public string Description => $"Add {Node.ControlType.XamlName}";

    public DesignerDocument Apply(DesignerDocument document) =>
        DocumentEditor.Add(document, ParentId, Node, Index);
}

public sealed record RemoveElementCommand(ElementId ElementId) : IDocumentCommand
{
    public string Description => $"Remove {ElementId}";

    public DesignerDocument Apply(DesignerDocument document) =>
        DocumentEditor.Remove(document, ElementId);
}

public sealed record ReparentElementCommand(
    ElementId ElementId,
    ElementId DestinationParentId,
    int DestinationIndex = -1,
    RectD? Bounds = null) : IDocumentCommand
{
    public string Description => $"Move {ElementId}";

    public DesignerDocument Apply(DesignerDocument document) =>
        DocumentEditor.Reparent(document, ElementId, DestinationParentId, DestinationIndex, Bounds);
}

public sealed record SetPropertyCommand(
    ElementId ElementId,
    string PropertyName,
    DesignerValue? Value) : IDocumentCommand
{
    public string Description => $"Set {PropertyName}";

    public DesignerDocument Apply(DesignerDocument document) =>
        DocumentEditor.SetProperty(document, ElementId, PropertyName, Value);
}

public sealed record SetBoundsCommand(ElementId ElementId, RectD? Bounds) : IDocumentCommand
{
    public string Description => $"Resize {ElementId}";

    public DesignerDocument Apply(DesignerDocument document) =>
        DocumentEditor.SetBounds(document, ElementId, Bounds);
}
