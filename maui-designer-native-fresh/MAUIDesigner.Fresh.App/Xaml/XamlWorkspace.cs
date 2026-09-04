using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Xaml;

namespace MAUIDesigner.Fresh.App.Xaml;

public sealed class XamlWorkspace
{
    private readonly DesignerXamlReader _reader = new();
    private readonly DesignerXamlWriter _writer = new();
    private readonly MAUIDesigner.Fresh.Core.Xaml.IXamlTypeResolver _resolver;

    public XamlWorkspace(MAUIDesigner.Fresh.Core.Xaml.IXamlTypeResolver resolver)
    {
        _resolver = resolver;
    }

    public XamlReadResult Parse(string xaml) => _reader.Read(xaml, _resolver);

    public string Write(DesignerDocument document) => _writer.Write(document);
}
