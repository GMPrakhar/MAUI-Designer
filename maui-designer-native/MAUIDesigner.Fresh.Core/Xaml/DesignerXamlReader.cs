using System.Collections.Immutable;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Geometry;

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
            visualRoot = ReadNode(root, resolver, diagnostics, ids, ref nextId, null);
        }
        else
        {
            XElement? content = FindWrapperContent(root, rootResolution.ContentPropertyName);
            if (content is null)
            {
                diagnostics.Add(Diagnostic(root, $"'{root.Name.LocalName}' has no visual content."));
                return new XamlReadResult(null, diagnostics);
            }

            visualRoot = ReadNode(content, resolver, diagnostics, ids, ref nextId, null);
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

        ImmutableDictionary<string, string>? namespaces = ReadNamespaces(root, diagnostics);
        if (namespaces is null)
        {
            return new XamlReadResult(null, diagnostics);
        }

        var document = new DesignerDocument(visualRoot, namespaces, metadata);
        document.Validate();
        return new XamlReadResult(document, []);
    }

    private static DesignerNode? ReadNode(
        XElement element,
        IXamlTypeResolver resolver,
        List<XamlDiagnostic> diagnostics,
        HashSet<string> ids,
        ref int nextId,
        string? parentPropertyName)
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
        var occupiedVisualProperties = new HashSet<string>(StringComparer.Ordinal);

        foreach (XElement child in element.Elements())
        {
            if (!child.Name.LocalName.Contains('.', StringComparison.Ordinal))
            {
                if (!resolution.AcceptsChildren)
                {
                    diagnostics.Add(Diagnostic(
                        child,
                        $"'{element.Name.LocalName}' cannot contain visual children."));
                    continue;
                }

                AddChildNode(children, child, resolver, diagnostics, ids, ref nextId, null);
                continue;
            }

            string memberName = child.Name.LocalName[(child.Name.LocalName.IndexOf('.') + 1)..];
            bool containsDefaultVisualContent =
                memberName is "Children" or "Content" ||
                string.Equals(memberName, resolution.ContentPropertyName, StringComparison.Ordinal);
            bool containsNamedVisualContent =
                !resolution.VisualPropertyNames.IsDefaultOrEmpty &&
                resolution.VisualPropertyNames.Contains(memberName, StringComparer.Ordinal);
            if (!containsDefaultVisualContent && !containsNamedVisualContent)
            {
                preserved.Add(ToFragment(child));
                continue;
            }

            XElement[] nestedElements = child.Elements().ToArray();
            if (containsNamedVisualContent && nestedElements.Length > 1)
            {
                diagnostics.Add(Diagnostic(
                    child,
                    $"Visual property '{memberName}' accepts only one child."));
                continue;
            }

            if (containsNamedVisualContent &&
                nestedElements.Length > 0 &&
                !occupiedVisualProperties.Add(memberName))
            {
                diagnostics.Add(Diagnostic(
                    child,
                    $"Visual property '{memberName}' is assigned more than once."));
                continue;
            }

            if (containsDefaultVisualContent && !resolution.AcceptsChildren)
            {
                diagnostics.Add(Diagnostic(
                    child,
                    $"'{element.Name.LocalName}' cannot contain visual children."));
                continue;
            }

            foreach (XElement nested in nestedElements)
            {
                AddChildNode(
                    children,
                    nested,
                    resolver,
                    diagnostics,
                    ids,
                    ref nextId,
                    containsDefaultVisualContent ? null : memberName);
            }
        }

        RectD? bounds = TryReadAbsoluteBounds(properties, out RectD parsedBounds)
            ? parsedBounds
            : null;
        return new DesignerNode(
            new ElementId(id),
            resolution.Type,
            properties,
            children.ToImmutable(),
            bounds,
            preservedContent: preserved.ToImmutable(),
            parentPropertyName: parentPropertyName);
    }

    private static ImmutableDictionary<string, string>? ReadNamespaces(
        XElement root,
        List<XamlDiagnostic> diagnostics)
    {
        var namespaces = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (XAttribute declaration in root
                     .DescendantsAndSelf()
                     .Attributes()
                     .Where(attribute => attribute.IsNamespaceDeclaration))
        {
            string prefix = declaration.Name.LocalName == "xmlns"
                ? string.Empty
                : declaration.Name.LocalName;
            if (prefix.Length == 0 && declaration.Parent != root)
            {
                continue;
            }

            if (namespaces.TryGetValue(prefix, out string? existing) &&
                !string.Equals(existing, declaration.Value, StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic(
                    declaration.Parent!,
                    $"Namespace prefix '{prefix}' resolves to multiple URIs."));
                return null;
            }

            namespaces[prefix] = declaration.Value;
        }

        return namespaces.ToImmutable();
    }

    private static bool TryReadAbsoluteBounds(
        IReadOnlyDictionary<string, DesignerValue> properties,
        out RectD bounds)
    {
        bounds = default;
        if (!properties.TryGetValue("AbsoluteLayout.LayoutBounds", out DesignerValue? value) ||
            (properties.TryGetValue("AbsoluteLayout.LayoutFlags", out DesignerValue? flags) &&
             !flags.Text.Equals("None", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string[] parts = value.Text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double width) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double height) ||
            !double.IsFinite(x) ||
            !double.IsFinite(y) ||
            !double.IsFinite(width) ||
            !double.IsFinite(height) ||
            width < 0 ||
            height < 0)
        {
            return false;
        }

        bounds = new RectD(x, y, width, height);
        return true;
    }

    private static void AddChildNode(
        ImmutableArray<DesignerNode>.Builder children,
        XElement childElement,
        IXamlTypeResolver resolver,
        List<XamlDiagnostic> diagnostics,
        HashSet<string> ids,
        ref int nextId,
        string? parentPropertyName)
    {
        DesignerNode? childNode = ReadNode(
            childElement,
            resolver,
            diagnostics,
            ids,
            ref nextId,
            parentPropertyName);
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
