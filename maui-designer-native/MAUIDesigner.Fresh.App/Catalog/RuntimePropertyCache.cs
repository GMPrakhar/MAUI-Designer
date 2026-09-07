using System.Collections.Concurrent;
using System.Reflection;

namespace MAUIDesigner.Fresh.App.Catalog;

internal static class RuntimePropertyCache
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PublicProperties = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>>
        WritableProperties = new();

    public static IReadOnlyList<PropertyInfo> GetPublicProperties(Type type) =>
        PublicProperties.GetOrAdd(
            type,
            runtimeType => runtimeType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property =>
                    property.GetIndexParameters().Length == 0 &&
                    property.GetMethod is not null)
                .GroupBy(property => property.Name, StringComparer.Ordinal)
                .Select(group => group
                    .OrderBy(property => InheritanceDistance(
                        runtimeType,
                        property.DeclaringType))
                    .First())
                .ToArray());

    public static IReadOnlyDictionary<string, PropertyInfo> GetWritableProperties(Type type) =>
        WritableProperties.GetOrAdd(
            type,
            runtimeType => GetPublicProperties(runtimeType)
                .Where(property => property.SetMethod?.IsPublic == true)
                .ToDictionary(property => property.Name, StringComparer.Ordinal));

    private static int InheritanceDistance(Type runtimeType, Type? declaringType)
    {
        int distance = 0;
        for (Type? current = runtimeType; current is not null; current = current.BaseType)
        {
            if (current == declaringType)
            {
                return distance;
            }

            distance++;
        }

        return int.MaxValue;
    }
}
