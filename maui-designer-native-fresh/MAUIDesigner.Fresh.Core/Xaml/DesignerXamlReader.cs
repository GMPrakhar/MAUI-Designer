using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.Core.Xaml;

public sealed class DesignerXamlReader
{
    private const string XamlLanguageNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";

    public XamlReadResult Read(string xaml, IXamlTypeResolver resolver)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);
        ArgumentNullException.ThrowIfNull(resolver);

        XDocument xml;
        try
        {
            xml = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            return XamlReadResult.Failure(
                new XamlDiagnostic(exception.Message, exception.LineNumber, exception.LinePosition));
        }

        XElement? root = xml.Root;
        if (root is null)
        {
            return XamlReadResult.Failure(new XamlDiagnostic("The XAML document has no root element."));
        }

        var diagnostics = new List<XamlDiagnostic>();
        if (!TryResolve(root, resolver, diagnostics, out XamlTypeResolution? rootResolution) ||
            rootResolution is null)
        {
            return new XamlReadResult(null, diagnostics);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        int nextId = 0;
        DesignerNode? visualRoot;
        XamlDocumentMetadata? metadata = null;
        if (rootResolution.IsView)
        {
            visualRoot = ReadNode(root, resolver, diagnostics, ids, ref nextId);
        }
        else
        {
            XElement? content = FindWrapperContent(root, rootResolution.ContentPropertyName);
            if (content is null)
            {
                diagnostics.Add(Diagnostic(root, $"'{root.Name.LocalName}' has no visual content."));
                return new XamlReadResult(null, diagnostics);
            }

            visualRoot = ReadNode(content, resolver, diagnostics, ids, ref nextId);
            int contentIndex = root.Elements().ToList().FindIndex(element =>
                element == content || element.Descendants().Contains(content));
            XElement[] wrapperElements = root.Elements().ToArray();
            metadata = new XamlDocumentMetadata(
                rootResolution.Type,
                ReadAttributes(root),
                wrapperElements.Take(Math.Max(0, contentIndex)).Select(ToFragment).ToImmutableArray(),
                wrapperElements.Skip(contentIndex + 1).Select(ToFragment).ToImmutableArray());
        }

        if (visualRoot is null || diagnostics.Count > 0)
        {
            return new XamlReadResult(null, diagnostics);
        }

        ImmutableDictionary<string, string> namespaces = root.Attributes()
            .Where(attribute => attribute.IsNamespaceDeclaration)
            .ToImmutableDictionary(
                attribute => attribute.Name.LocalName == "xmlns" ? string.Empty : attribute.Name.LocalName,
                attribute => attribute.Value,
                StringComparer.Ordinal);
        var document = new DesignerDocument(visualRoot, namespaces, metadata);
        document.Validate();
        return new XamlReadResult(document, []);
    }

    private static DesignerNode? ReadNode(
        XElement element,
        IXamlTypeResolver resolver,
        List<XamlDiagnostic> diagnostics,
        HashSet<string> ids,
        ref int nextId)
    {
        if (!TryResolve(element, resolver, diagnostics, out XamlTypeResolution? resolution) ||
            resolution is null)
        {
            return null;
        }

        if (!resolution.IsView)
        {
            diagnostics.Add(Diagnostic(element, $"'{element.Name.LocalName}' is not a visual control."));
            return null;
        }

        ImmutableDictionary<string, DesignerValue> properties = ReadAttributes(element);
        string requestedId = properties.TryGetValue("x:Name", out DesignerValue? name)
            ? name.Text
            : $"{resolution.Type.XamlName.ToLowerInvariant()}-{++nextId}";
        string id = MakeUniqueId(requestedId, ids);
        var children = ImmutableArray.CreateBuilder<DesignerNode>();
        var preserved = ImmutableArray.CreateBuilder<XamlSyntaxFragment>();

        foreach (XElement child in element.Elements())
        {
            if (!child.Name.LocalName.Contains('.', StringComparison.Ordinal))
            {
                AddChildNode(children, child, resolver, diagnostics, ids, ref nextId);
                continue;
            }

            string memberName = child.Name.LocalName[(child.Name.LocalName.IndexOf('.') + 1)..];
            bool containsVisualContent =
                memberName is "Children" or "Content" ||
                string.Equals(memberName, resolution.ContentPropertyName, StringComparison.Ordinal);
            if (!containsVisualContent)
            {
                preserved.Add(ToFragment(child));
                continue;
            }

            foreach (XElement nested in child.Elements())
            {
                AddChildNode(children, nested, resolver, diagnostics, ids, ref nextId);
            }
        }

        return new DesignerNode(
            new ElementId(id),
            resolution.Type,
            properties,
            children.ToImmutable(),
            preservedContent: preserved.ToImmutable());
    }

    private static void AddChildNode(
        ImmutableArray<DesignerNode>.Builder children,
        XElement childElement,
        IXamlTypeResolver resolver,
        List<XamlDiagnostic> diagnostics,
        HashSet<string> ids,
        ref int nextId)
    {
        DesignerNode? childNode = ReadNode(childElement, resolver, diagnostics, ids, ref nextId);
        if (childNode is not null)
        {
            children.Add(childNode);
        }
    }

    private static ImmutableDictionary<string, DesignerValue> ReadAttributes(XElement element)
    {
        var attributes = ImmutableDictionary.CreateBuilder<string, DesignerValue>(StringComparer.Ordinal);
        foreach (XAttribute attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
        {
            string prefix = element.GetPrefixOfNamespace(attribute.Name.Namespace) ?? string.Empty;
            string name = prefix.Length == 0
                ? attribute.Name.LocalName
                : $"{prefix}:{attribute.Name.LocalName}";
            DesignerValueKind kind = attribute.Value.StartsWith('{') && attribute.Value.EndsWith('}')
                ? DesignerValueKind.MarkupExtension
                : DesignerValueKind.Literal;
            attributes[name] = new DesignerValue(attribute.Value, kind);
        }

        return attributes.ToImmutable();
    }

    private static XElement? FindWrapperContent(XElement wrapper, string? contentPropertyName)
    {
        XElement? direct = wrapper.Elements()
            .FirstOrDefault(element => !element.Name.LocalName.Contains('.', StringComparison.Ordinal));
        if (direct is not null)
        {
            return direct;
        }

        return wrapper.Elements()
            .Where(element => element.Name.LocalName.Contains('.', StringComparison.Ordinal))
            .Where(element =>
            {
                string member = element.Name.LocalName[(element.Name.LocalName.IndexOf('.') + 1)..];
                return member == "Content" ||
                    string.Equals(member, contentPropertyName, StringComparison.Ordinal);
            })
            .SelectMany(element => element.Elements())
            .FirstOrDefault();
    }

    private static bool TryResolve(
        XElement element,
        IXamlTypeResolver resolver,
        List<XamlDiagnostic> diagnostics,
        out XamlTypeResolution? resolution)
    {
        if (resolver.TryResolve(element.Name.NamespaceName, element.Name.LocalName, out resolution))
        {
            return true;
        }

        diagnostics.Add(Diagnostic(
            element,
            $"Control '{element.Name}' could not be resolved from the registered assemblies."));
        return false;
    }

    private static string MakeUniqueId(string requested, HashSet<string> ids)
    {
        string normalized = string.IsNullOrWhiteSpace(requested)
            ? "element"
            : new string(requested.Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray());
        string candidate = normalized;
        int suffix = 1;
        while (!ids.Add(candidate))
        {
            candidate = $"{normalized}-{++suffix}";
        }

        return candidate;
    }

    private static XamlSyntaxFragment ToFragment(XElement element) =>
        new(element.ToString(SaveOptions.DisableFormatting));

    private static XamlDiagnostic Diagnostic(XElement element, string message)
    {
        var line = (IXmlLineInfo)element;
        return new XamlDiagnostic(
            message,
            line.HasLineInfo() ? line.LineNumber : null,
            line.HasLineInfo() ? line.LinePosition : null);
    }
}
