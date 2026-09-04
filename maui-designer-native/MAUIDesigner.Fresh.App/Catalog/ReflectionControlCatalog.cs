using System.Collections.Immutable;
using System.Reflection;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.Catalog;

public sealed class ReflectionControlCatalog : IControlCatalog
{
    private const string MauiXamlNamespace = "http://schemas.microsoft.com/dotnet/2021/maui";
    private readonly IServiceProvider _services;
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<IServiceProvider, View>> _factories = [];
    private ImmutableDictionary<string, ControlDescriptor> _byId =
        ImmutableDictionary<string, ControlDescriptor>.Empty;

    public ReflectionControlCatalog(IServiceProvider services)
    {
        _services = services;
    }

    public event EventHandler? Changed;

    public ImmutableArray<ControlDescriptor> Controls =>
        _byId.Values
            .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.Ordinal)
            .ToImmutableArray();

    public void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        lock (_gate)
        {
            var next = _byId.ToBuilder();
            Type[] types = GetLoadableTypes(assembly).ToArray();
            foreach (Type type in types)
            {
                if (!IsDiscoverableView(type))
                {
                    continue;
                }

                if (!TryCreateFactory(type, out Func<IServiceProvider, View>? factory) ||
                    factory is null)
                {
                    continue;
                }

                ControlDescriptor descriptor = CreateDescriptor(type, factory);
                next[descriptor.Id.Key] = descriptor;
            }

            _byId = next.ToImmutable();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterFactory<TView>(Func<IServiceProvider, TView> factory)
        where TView : View
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            _factories[typeof(TView)] = services => factory(services);
            ControlDescriptor descriptor = CreateDescriptor(typeof(TView), _factories[typeof(TView)]);
            _byId = _byId.SetItem(descriptor.Id.Key, descriptor);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool TryGet(ControlTypeId id, out ControlDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _byId.TryGetValue(id.Key, out descriptor);
    }

    public View Create(ControlTypeId id)
    {
        if (!TryGet(id, out ControlDescriptor? descriptor) || descriptor is null)
        {
            throw new KeyNotFoundException($"Control '{id.Key}' is not registered.");
        }

        return descriptor.Factory(_services);
    }

    private bool TryCreateFactory(Type type, out Func<IServiceProvider, View>? factory)
    {
        if (_factories.TryGetValue(type, out factory))
        {
            return true;
        }

        ConstructorInfo? constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            factory = null;
            return false;
        }

        factory = _ => (View)constructor.Invoke(null);
        return true;
    }

    private static ControlDescriptor CreateDescriptor(
        Type type,
        Func<IServiceProvider, View> factory)
    {
        string assemblyName = type.Assembly.GetName().Name
            ?? throw new InvalidOperationException($"Assembly name unavailable for '{type}'.");
        bool isMaui = type.Assembly == typeof(View).Assembly ||
            type.Namespace?.StartsWith("Microsoft.Maui.Controls", StringComparison.Ordinal) == true;
        string xamlNamespace = isMaui
            ? MauiXamlNamespace
            : ResolveAssemblyXamlNamespace(type) ??
              $"clr-namespace:{type.Namespace};assembly={assemblyName}";
        var id = new ControlTypeId(assemblyName, type.FullName!, xamlNamespace, type.Name);
        string category = GetCategory(type);
        bool acceptsChildren = AcceptsChildren(type);
        ImmutableArray<PropertyDescriptor> properties = DiscoverProperties(type);

        return new ControlDescriptor(
            id,
            type,
            SplitPascalCase(type.Name),
            category,
            acceptsChildren,
            factory,
            properties);
    }

    private static ImmutableArray<PropertyDescriptor> DiscoverProperties(Type type)
    {
        string? contentProperty = VisualContentProperty.Find(type)?.Name;

        var bindableNames = type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => typeof(BindableProperty).IsAssignableFrom(field.FieldType))
            .Select(field => field.Name.EndsWith("Property", StringComparison.Ordinal)
                ? field.Name[..^"Property".Length]
                : field.Name)
            .ToHashSet(StringComparer.Ordinal);

        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is not null)
            .Select(property => new PropertyDescriptor(
                property.Name,
                property.PropertyType,
                bindableNames.Contains(property.Name),
                string.Equals(property.Name, contentProperty, StringComparison.Ordinal),
                false,
                property.SetMethod is null || !property.SetMethod.IsPublic))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static bool IsDiscoverableView(Type type) =>
        type.IsPublic &&
        !type.IsAbstract &&
        !type.IsGenericTypeDefinition &&
        type.Namespace?.StartsWith(
            "Microsoft.Maui.Controls.Compatibility",
            StringComparison.Ordinal) != true &&
        typeof(View).IsAssignableFrom(type);

    private static bool AcceptsChildren(Type type) =>
        typeof(Layout).IsAssignableFrom(type) ||
        VisualContentProperty.Find(type) is not null;

    private static string GetCategory(Type type)
    {
        if (typeof(Layout).IsAssignableFrom(type))
        {
            return "Layouts";
        }

        if (typeof(InputView).IsAssignableFrom(type) ||
            type == typeof(Button) ||
            type == typeof(CheckBox) ||
            type == typeof(Switch) ||
            type == typeof(Slider) ||
            type == typeof(Stepper) ||
            type == typeof(DatePicker) ||
            type == typeof(TimePicker) ||
            type == typeof(SearchBar))
        {
            return "Input";
        }

        if (type.Name.Contains("View", StringComparison.Ordinal))
        {
            return "Data and collections";
        }

        return "Display";
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static string? ResolveAssemblyXamlNamespace(Type type)
    {
        string? typeNamespace = type.Namespace;
        if (typeNamespace is null)
        {
            return null;
        }

        return type.Assembly.CustomAttributes
            .Where(attribute => attribute.AttributeType.Name == "XmlnsDefinitionAttribute")
            .Where(attribute => attribute.ConstructorArguments.Count >= 2)
            .Select(attribute => new
            {
                XmlNamespace = attribute.ConstructorArguments[0].Value as string,
                ClrNamespace = attribute.ConstructorArguments[1].Value as string
            })
            .Where(mapping =>
                mapping.XmlNamespace is not null &&
                mapping.ClrNamespace is not null &&
                (typeNamespace == mapping.ClrNamespace ||
                 typeNamespace.StartsWith($"{mapping.ClrNamespace}.", StringComparison.Ordinal)))
            .OrderByDescending(mapping => mapping.ClrNamespace!.Length)
            .Select(mapping => mapping.XmlNamespace)
            .FirstOrDefault();
    }

    private static string SplitPascalCase(string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var result = new System.Text.StringBuilder(value.Length + 8);
        result.Append(value[0]);
        for (int index = 1; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsUpper(current) && !char.IsUpper(value[index - 1]))
            {
                result.Append(' ');
            }

            result.Append(current);
        }

        return result.ToString();
    }
}
