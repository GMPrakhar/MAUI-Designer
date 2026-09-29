using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using MAUIDesigner.Fresh.App.Hosting;
using Microsoft.Maui.Hosting;

namespace MAUIDesigner.Fresh.App.Catalog;

public sealed class DesignerPackageRuntime
{
    private const string StartupManifestArgument = "--designer-startup-manifest";
    private const string SyncfusionPackage = "Syncfusion.Maui.Core";
    private readonly PackageLoadContext _context = new();
    private readonly Dictionary<string, Assembly> _assemblies =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _diagnostics = [];

    public IReadOnlyCollection<Assembly> Assemblies => _assemblies.Values;

    public IReadOnlyList<string> Diagnostics => _diagnostics;

    public static (DesignerPackageRuntime Runtime, HostedProjectControls? ProjectControls)
        FromCommandLine()
    {
        var runtime = new DesignerPackageRuntime();
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, StartupManifestArgument);
        if (index < 0 || index + 1 >= arguments.Length)
        {
            return (runtime, null);
        }

        string path = arguments[index + 1];
        if (!File.Exists(path))
        {
            return (runtime, null);
        }

        var controls = JsonSerializer.Deserialize<HostedProjectControls>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (controls is not null)
        {
            try
            {
                runtime.Load(controls);
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or
                FileLoadException or
                BadImageFormatException or
                TypeLoadException)
            {
                runtime._diagnostics.Add(
                    "Third-party control loading failed: " +
                    (exception.InnerException?.Message ?? exception.Message));
            }
        }

        return (runtime, controls);
    }

    public IReadOnlyList<Assembly> Load(HostedProjectControls projectControls)
    {
        _context.Add(projectControls.Assemblies.Select(assembly => assembly.Path));
        var roots = projectControls.Assemblies
            .Where(assembly => assembly.IsRoot)
            .Select(assembly => assembly.Path)
            .Concat(projectControls.StartupMethods.Select(method =>
                projectControls.Assemblies.FirstOrDefault(assembly =>
                    string.Equals(
                        Path.GetFileNameWithoutExtension(assembly.Path),
                        method.Assembly,
                        StringComparison.OrdinalIgnoreCase))?.Path))
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var loaded = new List<Assembly>();
        foreach (string path in roots)
        {
            Assembly assembly = LoadAssembly(path);
            if (loaded.All(candidate => candidate != assembly))
            {
                loaded.Add(assembly);
            }
        }

        return loaded;
    }

    public Assembly LoadAssembly(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        string fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Control assembly was not found.", fullPath);
        }

        _context.Add([fullPath]);
        string name = AssemblyName.GetAssemblyName(fullPath).Name
            ?? Path.GetFileNameWithoutExtension(fullPath);
        if (_assemblies.TryGetValue(name, out Assembly? existing))
        {
            return existing;
        }

        Assembly assembly = _context.LoadFromAssemblyPath(fullPath);
        _assemblies[name] = assembly;
        return assembly;
    }

    public void Configure(MauiAppBuilder builder, HostedProjectControls? projectControls)
    {
        if (projectControls is null)
        {
            return;
        }

        foreach (HostedStartupMethod startup in projectControls.StartupMethods)
        {
            try
            {
                if (!_assemblies.TryGetValue(startup.Assembly, out Assembly? assembly))
                {
                    throw new InvalidOperationException(
                        $"Startup assembly '{startup.Assembly}' was not loaded.");
                }

                Type type = assembly.GetType(startup.Type, throwOnError: true)!;
                MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .SingleOrDefault(candidate =>
                    {
                        ParameterInfo[] parameters = candidate.GetParameters();
                        return candidate.Name == startup.Method &&
                            parameters.Length == 1 &&
                            parameters[0].ParameterType.IsAssignableFrom(typeof(MauiAppBuilder));
                    })
                    ?? throw new MissingMethodException(startup.Type, startup.Method);
                int serviceCount = builder.Services.Count;
                method.Invoke(null, [builder]);
                RemoveUnsupportedDesignerInitializers(
                    builder,
                    startup,
                    assembly,
                    serviceCount);
            }
            catch (Exception exception)
            {
                _diagnostics.Add(
                    $"{startup.Package} startup registration failed: " +
                    $"{exception.InnerException?.Message ?? exception.Message}");
            }
        }
    }

    private static void RemoveUnsupportedDesignerInitializers(
        MauiAppBuilder builder,
        HostedStartupMethod startup,
        Assembly assembly,
        int serviceCount)
    {
        if (!string.Equals(
                startup.Package,
                SyncfusionPackage,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Syncfusion's Windows initializer loads a package-relative XAML resource
        // through ms-appx. Project controls are loaded from the NuGet cache rather
        // than compiled into the designer package, so WinUI terminates the process
        // while resolving that URI. Its handler and font registrations remain valid.
        for (int index = builder.Services.Count - 1; index >= serviceCount; index--)
        {
            if (builder.Services[index].ServiceType == typeof(IMauiInitializeScopedService) &&
                builder.Services[index].ImplementationType?.Assembly == assembly)
            {
                builder.Services.RemoveAt(index);
            }
        }
    }

    private sealed class PackageLoadContext : AssemblyLoadContext
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, string> _paths =
            new(StringComparer.OrdinalIgnoreCase);

        public PackageLoadContext()
            : base("MAUIDesigner:project-controls")
        {
        }

        public void Add(IEnumerable<string> paths)
        {
            lock (_gate)
            {
                foreach (string path in paths.Where(File.Exists))
                {
                    string? name = AssemblyName.GetAssemblyName(path).Name;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _paths[name] = Path.GetFullPath(path);
                    }
                }
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            Assembly? shared = Default.Assemblies.FirstOrDefault(assembly =>
                AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName));
            if (shared is not null)
            {
                return shared;
            }

            lock (_gate)
            {
                return assemblyName.Name is not null &&
                    _paths.TryGetValue(assemblyName.Name, out string? path)
                        ? LoadFromAssemblyPath(path)
                        : null;
            }
        }
    }
}

public sealed class AssemblyExtensionLoader
{
    private readonly IControlCatalog _catalog;
    private readonly DesignerPackageRuntime _runtime;

    public AssemblyExtensionLoader(IControlCatalog catalog, DesignerPackageRuntime runtime)
    {
        _catalog = catalog;
        _runtime = runtime;
    }

    public AssemblyExtensionLoader(IControlCatalog catalog)
        : this(catalog, new DesignerPackageRuntime())
    {
    }

    public IReadOnlyList<string> Diagnostics => _runtime.Diagnostics;

    public ExtensionLoadResult Load(string assemblyPath)
    {
        int before = _catalog.Controls.Length;
        Assembly assembly = _runtime.LoadAssembly(assemblyPath);
        _catalog.RegisterAssembly(assembly);
        return new ExtensionLoadResult(
            assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(assemblyPath),
            _catalog.Controls.Length - before);
    }

    public IReadOnlyList<ExtensionLoadResult> Load(HostedProjectControls projectControls)
    {
        var results = new List<ExtensionLoadResult>();
        foreach (Assembly assembly in _runtime.Load(projectControls))
        {
            int before = _catalog.Controls.Length;
            _catalog.RegisterAssembly(assembly);
            results.Add(new ExtensionLoadResult(
                assembly.GetName().Name ?? string.Empty,
                _catalog.Controls.Length - before));
        }

        return results;
    }
}

public sealed record ExtensionLoadResult(string AssemblyName, int ControlsAdded);
