using System.Collections.Immutable;
using System.Globalization;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.Core.Documents;

namespace MAUIDesigner.Fresh.App.PropertyEditing;

public static class PropertyInspectorDescriptorSource
{
    private static readonly ImmutableArray<PropertyDescriptor> GridPlacementProperties =
    [
        AttachedInteger("Grid.Row"),
        AttachedInteger("Grid.Column"),
        AttachedInteger("Grid.RowSpan"),
        AttachedInteger("Grid.ColumnSpan")
    ];

    public static IReadOnlyList<PropertyDescriptor> Compose(
        ControlDescriptor descriptor,
        DesignerNode node,
        DesignerNode? parent)
    {
        var properties = descriptor.Properties.ToList();
        var names = properties.Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (parent?.ControlType.XamlName == nameof(Grid))
        {
            foreach (PropertyDescriptor property in GridPlacementProperties)
            {
                if (names.Add(property.Name))
                {
                    properties.Add(property);
                }
            }
        }

        foreach (string name in node.Properties.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (names.Add(name))
            {
                properties.Add(new PropertyDescriptor(
                    name,
                    typeof(string),
                    IsBindable: false,
                    IsContent: false,
                    IsAttached: name.Contains('.', StringComparison.Ordinal),
                    IsReadOnly: false));
            }
        }

        return properties;
    }

    public static string? DefaultValue(string propertyName) =>
        propertyName switch
        {
            "Grid.Row" or "Grid.Column" => "0",
            "Grid.RowSpan" or "Grid.ColumnSpan" => "1",
            _ => null
        };

    public static bool TryNormalize(
        PropertyDescriptor property,
        string? value,
        out string? normalized,
        out string? error)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        error = null;
        int minimum = property.Name switch
        {
            "Grid.Row" or "Grid.Column" => 0,
            "Grid.RowSpan" or "Grid.ColumnSpan" => 1,
            _ => int.MinValue
        };
        if (minimum == int.MinValue || normalized is null)
        {
            return true;
        }

        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
            parsed < minimum)
        {
            error = $"{property.Name} must be a whole number of at least {minimum}.";
            return false;
        }

        normalized = parsed.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static PropertyDescriptor AttachedInteger(string name) =>
        new(
            name,
            typeof(int),
            IsBindable: true,
            IsContent: false,
            IsAttached: true,
            IsReadOnly: false);
}
