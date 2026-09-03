namespace MAUIDesigner.Fresh.Core.Documents;

public enum DesignerValueKind
{
    Literal,
    MarkupExtension,
    PropertyElement,
    Raw
}

public sealed record DesignerValue(string Text, DesignerValueKind Kind = DesignerValueKind.Literal)
{
    public static DesignerValue Literal(string text) => new(text);
}
