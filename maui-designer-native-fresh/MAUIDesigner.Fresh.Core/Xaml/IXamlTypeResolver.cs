using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.Core.Xaml;

public interface IXamlTypeResolver
{
    bool TryResolve(string xamlNamespace, string localName, out XamlTypeResolution? resolution);
}

public sealed record XamlTypeResolution(
    ControlTypeId Type,
    bool IsView,
    string? ContentPropertyName);
