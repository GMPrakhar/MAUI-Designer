using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.Core.Xaml;

public sealed record XamlDiagnostic(string Message, int? Line = null, int? Column = null);

public sealed record XamlReadResult(DesignerDocument? Document, IReadOnlyList<XamlDiagnostic> Diagnostics)
{
    public bool Success => Document is not null && Diagnostics.Count == 0;

    public static XamlReadResult Failure(params XamlDiagnostic[] diagnostics) =>
        new(null, diagnostics);
}
