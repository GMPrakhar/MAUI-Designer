namespace MAUIDesigner.Fresh.Core.Documents;

public readonly record struct ElementId
{
    public ElementId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
