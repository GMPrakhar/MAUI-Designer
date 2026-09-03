using System.Reflection;
using System.Runtime.Loader;

namespace MAUIDesigner.Fresh.App.Catalog;

public sealed class AssemblyExtensionLoader
{
    private readonly IControlCatalog _catalog;
    private readonly List<AssemblyLoadContext> _contexts = [];

    public AssemblyExtensionLoader(IControlCatalog catalog)
    {
        _catalog = catalog;
    }

    public ExtensionLoadResult Load(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        string fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Control assembly was not found.", fullPath);
        }

        int before = _catalog.Controls.Length;
        var context = new DesignerExtensionLoadContext(fullPath);
        Assembly assembly = context.LoadFromAssemblyPath(fullPath);
        _catalog.RegisterAssembly(assembly);
        _contexts.Add(context);
        return new ExtensionLoadResult(
            assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(fullPath),
            _catalog.Controls.Length - before);
    }

    private sealed class DesignerExtensionLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public DesignerExtensionLoadContext(string componentAssemblyPath)
            : base($"MAUIDesigner:{Path.GetFileNameWithoutExtension(componentAssemblyPath)}")
        {
            _resolver = new AssemblyDependencyResolver(componentAssemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            Assembly? shared = Default.Assemblies.FirstOrDefault(assembly =>
                AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
            if (shared is not null)
            {
                return shared;
            }

            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}

public sealed record ExtensionLoadResult(string AssemblyName, int ControlsAdded);
