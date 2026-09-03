using System.Xml.Linq;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.Core.Xaml;

public sealed class DesignerXamlWriter
{
    public string Write(DesignerDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();

        XElement visualRoot = WriteNode(document.Root, document.Namespaces);
        XElement outputRoot = document.XamlMetadata is null
            ? visualRoot
            : WriteWrapper(document.XamlMetadata, visualRoot, document.Namespaces);
        AddNamespaces(outputRoot, document.Namespaces);
        return new XDocument(new XDeclaration("1.0", "utf-8", null), outputRoot)
            .ToString(SaveOptions.None);
    }

    private static XElement WriteNode(
        DesignerNode node,
        IReadOnlyDictionary<string, string> namespaces)
    {
        var element = new XElement(
            XNamespace.Get(node.ControlType.XamlNamespace) + node.ControlType.XamlName);
        WriteAttributes(element, node.Properties, namespaces);
        foreach (XamlSyntaxFragment fragment in node.PreservedContent)
        {
            element.Add(XElement.Parse(fragment.Xml, LoadOptions.PreserveWhitespace));
        }

        foreach (DesignerNode child in node.Children)
        {
            element.Add(WriteNode(child, namespaces));
        }

        return element;
    }

    private static XElement WriteWrapper(
        XamlDocumentMetadata metadata,
        XElement visualRoot,
        IReadOnlyDictionary<string, string> namespaces)
    {
        var wrapper = new XElement(
            XNamespace.Get(metadata.WrapperType.XamlNamespace) + metadata.WrapperType.XamlName);
        WriteAttributes(wrapper, metadata.Attributes, namespaces);
        foreach (XamlSyntaxFragment fragment in metadata.BeforeContent)
        {
            wrapper.Add(XElement.Parse(fragment.Xml, LoadOptions.PreserveWhitespace));
        }

        wrapper.Add(visualRoot);
        foreach (XamlSyntaxFragment fragment in metadata.AfterContent)
        {
            wrapper.Add(XElement.Parse(fragment.Xml, LoadOptions.PreserveWhitespace));
        }

        return wrapper;
    }

    private static void WriteAttributes(
        XElement element,
        IReadOnlyDictionary<string, DesignerValue> attributes,
        IReadOnlyDictionary<string, string> namespaces)
    {
        foreach ((string name, DesignerValue value) in attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            int separator = name.IndexOf(':');
            if (separator < 0)
            {
                element.SetAttributeValue(name, value.Text);
                continue;
            }

            string prefix = name[..separator];
            string localName = name[(separator + 1)..];
            if (!namespaces.TryGetValue(prefix, out string? namespaceUri))
            {
                throw new InvalidOperationException($"Namespace prefix '{prefix}' is not declared.");
            }

            element.SetAttributeValue(XNamespace.Get(namespaceUri) + localName, value.Text);
        }
    }

    private static void AddNamespaces(
        XElement element,
        IReadOnlyDictionary<string, string> namespaces)
    {
        foreach ((string prefix, string uri) in namespaces.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (prefix.Length == 0)
            {
                element.SetAttributeValue("xmlns", uri);
            }
            else
            {
                element.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
            }
        }
    }
}
