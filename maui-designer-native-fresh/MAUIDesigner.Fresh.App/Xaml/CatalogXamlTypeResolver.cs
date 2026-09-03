using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.Core.Documents;
using MAUIDesigner.Fresh.Core.Xaml;

namespace MAUIDesigner.Fresh.App.Xaml;

public sealed class CatalogXamlTypeResolver : MAUIDesigner.Fresh.Core.Xaml.IXamlTypeResolver
{
    private const string MauiNamespace = "http://schemas.microsoft.com/dotnet/2021/maui";
    private readonly IControlCatalog _catalog;

    public CatalogXamlTypeResolver(IControlCatalog catalog)
    {
        _catalog = catalog;
    }

    public bool TryResolve(
        string xamlNamespace,
        string localName,
        out XamlTypeResolution? resolution)
    {
        ControlDescriptor? descriptor = _catalog.Controls.FirstOrDefault(control =>
            control.Id.XamlNamespace == xamlNamespace &&
            control.Id.XamlName == localName);
        if (descriptor is not null)
        {
            resolution = new XamlTypeResolution(
                descriptor.Id,
                true,
                descriptor.Properties.FirstOrDefault(property => property.IsContent)?.Name);
            return true;
        }

        if (xamlNamespace == MauiNamespace && localName is "ContentPage" or "ContentView" or "Window")
        {
            var wrapper = new ControlTypeId(
                "Microsoft.Maui.Controls",
                $"Microsoft.Maui.Controls.{localName}",
                xamlNamespace,
                localName);
            resolution = new XamlTypeResolution(wrapper, localName == "ContentView", "Content");
            return true;
        }

        if (TryParseClrNamespace(xamlNamespace, out string? clrNamespace, out string? assemblyName))
        {
            var type = new ControlTypeId(
                assemblyName,
                $"{clrNamespace}.{localName}",
                xamlNamespace,
                localName);
            resolution = new XamlTypeResolution(type, true, null);
            return true;
        }

        resolution = null;
        return false;
    }

    private static bool TryParseClrNamespace(
        string xamlNamespace,
        out string clrNamespace,
        out string assemblyName)
    {
        clrNamespace = string.Empty;
        assemblyName = string.Empty;
        if (!xamlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            return false;
        }

        string[] segments = xamlNamespace.Split(';', StringSplitOptions.TrimEntries);
        clrNamespace = segments[0]["clr-namespace:".Length..];
        assemblyName = segments
            .Skip(1)
            .FirstOrDefault(segment => segment.StartsWith("assembly=", StringComparison.Ordinal))?
            ["assembly=".Length..] ?? clrNamespace;
        return clrNamespace.Length > 0 && assemblyName.Length > 0;
    }
}
