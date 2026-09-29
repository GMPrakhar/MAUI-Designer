namespace MAUIDesigner.Fresh.App.Catalog;

public static class ControlToolboxCategory
{
    public static string For(ControlDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        string mauiAssembly = typeof(View).Assembly.GetName().Name ?? "Microsoft.Maui.Controls";
        return string.Equals(
            descriptor.Id.AssemblyName,
            mauiAssembly,
            StringComparison.OrdinalIgnoreCase)
            ? descriptor.Category
            : descriptor.Id.AssemblyName;
    }
}
