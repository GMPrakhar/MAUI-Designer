using System.Reflection;

namespace MAUIDesigner.Fresh.App.Catalog;

internal static class VisualContentProperty
{
    public static PropertyInfo? Find(Type type)
    {
        string? contentPropertyName = GetContentPropertyName(type);
        return FindAll(type)
            .Where(property => property.Name == (contentPropertyName ?? "Content"))
            .FirstOrDefault();
    }

    public static IReadOnlyList<PropertyInfo> FindAll(Type type) =>
        type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.GetIndexParameters().Length == 0 &&
                property.CanWrite &&
                property.SetMethod?.IsPublic == true &&
                property.PropertyType != typeof(object) &&
                property.PropertyType.IsAssignableFrom(typeof(View)))
            .OrderByDescending(property => property.DeclaringType == type)
            .ThenBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

    private static string? GetContentPropertyName(Type type)
    {
        CustomAttributeData? contentAttribute = EnumerateTypeAttributes(type)
            .FirstOrDefault(attribute => attribute.AttributeType.Name == "ContentPropertyAttribute");
        string? name = contentAttribute?.NamedArguments
            .FirstOrDefault(argument => argument.MemberName == "Name")
            .TypedValue.Value as string;
        return name ??
            (contentAttribute?.ConstructorArguments.Count > 0
                ? contentAttribute.ConstructorArguments[0].Value as string
                : null);
    }

    private static IEnumerable<CustomAttributeData> EnumerateTypeAttributes(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            foreach (CustomAttributeData attribute in current.CustomAttributes)
            {
                yield return attribute;
            }
        }
    }
}
