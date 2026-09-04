using System.Collections.Immutable;

namespace MAUIDesigner.Fresh.Core.Documents;

public sealed record XamlDocumentMetadata(
    ControlTypeId WrapperType,
    ImmutableDictionary<string, DesignerValue> Attributes,
    ImmutableArray<XamlSyntaxFragment> BeforeContent,
    ImmutableArray<XamlSyntaxFragment> AfterContent);

public sealed record XamlSyntaxFragment(string Xml);
