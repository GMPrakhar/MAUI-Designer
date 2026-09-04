namespace MAUIDesigner.Fresh.Core.Documents;

public sealed record ControlTypeId
{
    public ControlTypeId(string assemblyName, string fullName, string xamlNamespace, string xamlName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(xamlNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(xamlName);
        AssemblyName = assemblyName;
        FullName = fullName;
        XamlNamespace = xamlNamespace;
        XamlName = xamlName;
    }

    public string AssemblyName { get; }

    public string FullName { get; }

    public string XamlNamespace { get; }

    public string XamlName { get; }

    public string Key => $"{AssemblyName}:{FullName}";
}
