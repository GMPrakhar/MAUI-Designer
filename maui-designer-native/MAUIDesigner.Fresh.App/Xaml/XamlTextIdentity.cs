namespace MAUIDesigner.Fresh.App.Xaml;

public static class XamlTextIdentity
{
    public static string Normalize(string? value) =>
        (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}
