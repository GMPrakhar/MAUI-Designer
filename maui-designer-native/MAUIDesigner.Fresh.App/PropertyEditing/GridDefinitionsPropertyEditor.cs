using System.Globalization;
using MAUIDesigner.Fresh.App.Catalog;

namespace MAUIDesigner.Fresh.App.PropertyEditing;

public enum GridTrackUnitType
{
    Auto,
    Star,
    Absolute
}

public readonly record struct GridTrackDefinition(GridTrackUnitType UnitType, double Value)
{
    public static GridTrackDefinition Auto => new(GridTrackUnitType.Auto, 1);
}

public static class GridDefinitionSerializer
{
    public static bool TryParse(string? text, out GridTrackDefinition[] definitions)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            definitions = [];
            return true;
        }

        string[] parts = text.Split(',', StringSplitOptions.TrimEntries);
        definitions = new GridTrackDefinition[parts.Length];
        for (int index = 0; index < parts.Length; index++)
        {
            if (!TryParseTrack(parts[index], out definitions[index]))
            {
                definitions = [];
                return false;
            }
        }

        return true;
    }

    public static string Serialize(IEnumerable<GridTrackDefinition> definitions) =>
        string.Join(",", definitions.Select(Serialize));

    public static string Serialize(GridTrackDefinition definition) =>
        definition.UnitType switch
        {
            GridTrackUnitType.Auto => "Auto",
            GridTrackUnitType.Star when definition.Value == 1 => "*",
            GridTrackUnitType.Star =>
                $"{definition.Value.ToString("G", CultureInfo.InvariantCulture)}*",
            GridTrackUnitType.Absolute =>
                definition.Value.ToString("G", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(definition))
        };

    private static bool TryParseTrack(string text, out GridTrackDefinition definition)
    {
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            definition = GridTrackDefinition.Auto;
            return true;
        }

        bool isStar = text.EndsWith('*');
        ReadOnlySpan<char> numeric = isStar ? text.AsSpan(0, text.Length - 1) : text.AsSpan();
        double value;
        if (isStar && numeric.IsEmpty)
        {
            value = 1;
        }
        else if (!double.TryParse(
                     numeric,
                     NumberStyles.Float,
                     CultureInfo.InvariantCulture,
                     out value) ||
                 !double.IsFinite(value) ||
                 value < 0)
        {
            definition = default;
            return false;
        }

        definition = new GridTrackDefinition(
            isStar ? GridTrackUnitType.Star : GridTrackUnitType.Absolute,
            value);
        return true;
    }
}

public sealed class GridDefinitionCollectionModel
{
    private readonly List<GridTrackDefinition> _definitions;

    public GridDefinitionCollectionModel(string? value)
    {
        if (!GridDefinitionSerializer.TryParse(value, out GridTrackDefinition[] definitions))
        {
            throw new FormatException("The Grid definition contains an invalid length.");
        }

        _definitions = [.. definitions];
    }

    public IReadOnlyList<GridTrackDefinition> Definitions => _definitions;

    public string? Add()
    {
        _definitions.Add(new GridTrackDefinition(GridTrackUnitType.Star, 1));
        return Serialize();
    }

    public string? RemoveAt(int index)
    {
        if ((uint)index >= (uint)_definitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _definitions.RemoveAt(index);
        return Serialize();
    }

    public string? Replace(int index, GridTrackDefinition definition)
    {
        if ((uint)index >= (uint)_definitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _definitions[index] = definition;
        return Serialize();
    }

    private string? Serialize() =>
        _definitions.Count == 0 ? null : GridDefinitionSerializer.Serialize(_definitions);
}

public sealed class GridDefinitionsPropertyEditor : IPropertyEditor
{
    public bool CanEdit(PropertyDescriptor property)
    {
        Type type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;
        return type == typeof(RowDefinitionCollection) ||
            type == typeof(ColumnDefinitionCollection);
    }

    public View Create(PropertyEditorContext context)
    {
        bool rows = (Nullable.GetUnderlyingType(context.Property.ValueType) ??
            context.Property.ValueType) == typeof(RowDefinitionCollection);
        return new GridDefinitionCollectionEditor(context, rows);
    }
}

public sealed class GridDefinitionCollectionEditor : ContentView
{
    private static readonly string[] UnitNames = ["Auto", "Star", "Absolute"];
    private readonly PropertyEditorContext _context;
    private readonly bool _rows;
    private readonly List<GridTrackDefinition> _definitions;
    private readonly VerticalStackLayout _trackList;

    public GridDefinitionCollectionEditor(PropertyEditorContext context, bool rows)
    {
        _context = context;
        _rows = rows;
        if (!GridDefinitionSerializer.TryParse(context.Value, out GridTrackDefinition[] parsed))
        {
            parsed = [];
            context.ShowError($"{context.Property.Name} contains an invalid Grid length.");
        }

        _definitions = [.. parsed];
        _trackList = new VerticalStackLayout { Spacing = 5 };
        var addButton = new Button
        {
            AutomationId = $"{EditorAutomationId}-add",
            FontSize = 11,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start,
            Text = rows ? "+ Add row" : "+ Add column"
        };
        addButton.Clicked += (_, _) => AddTrack();

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Children = { _trackList, addButton }
        };
        RebuildRows();
    }

    public string EditorAutomationId => $"property-{_context.Property.Name}";

    public IReadOnlyList<GridTrackDefinition> Definitions => _definitions;

    public void AddTrack()
    {
        _definitions.Add(new GridTrackDefinition(GridTrackUnitType.Star, 1));
        RebuildRows();
        Commit();
    }

    public void RemoveTrack(int index)
    {
        if ((uint)index >= (uint)_definitions.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _definitions.RemoveAt(index);
        RebuildRows();
        Commit();
    }

    private void RebuildRows()
    {
        _trackList.Children.Clear();
        for (int index = 0; index < _definitions.Count; index++)
        {
            int trackIndex = index;
            GridTrackDefinition definition = _definitions[index];
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(54)),
                    new ColumnDefinition(new GridLength(88)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(34))
                },
                ColumnSpacing = 5
            };
            row.Add(new Label
            {
                FontSize = 10,
                Text = $"{(_rows ? "Row" : "Column")} {index + 1}",
                VerticalTextAlignment = TextAlignment.Center
            });

            var unitPicker = new Picker
            {
                AutomationId = $"{EditorAutomationId}-{index}-type",
                FontSize = 10,
                HeightRequest = 34,
                ItemsSource = UnitNames,
                SelectedIndex = (int)definition.UnitType
            };
            row.Add(unitPicker, 1);

            var valueEntry = new Entry
            {
                AutomationId = $"{EditorAutomationId}-{index}-value",
                FontSize = 10,
                HeightRequest = 34,
                IsEnabled = definition.UnitType != GridTrackUnitType.Auto,
                Keyboard = Keyboard.Numeric,
                Placeholder = definition.UnitType == GridTrackUnitType.Star ? "Weight" : "Pixels",
                Text = definition.Value.ToString("G", CultureInfo.InvariantCulture)
            };
            row.Add(valueEntry, 2);

            var removeButton = new Button
            {
                AutomationId = $"{EditorAutomationId}-{index}-remove",
                FontSize = 13,
                HeightRequest = 32,
                Padding = 0,
                Text = "−"
            };
            row.Add(removeButton, 3);

            unitPicker.SelectedIndexChanged += (_, _) =>
            {
                if (unitPicker.SelectedIndex < 0)
                {
                    return;
                }

                GridTrackUnitType unit = (GridTrackUnitType)unitPicker.SelectedIndex;
                double value = unit == GridTrackUnitType.Auto
                    ? 1
                    : _definitions[trackIndex].Value;
                if (unit != GridTrackUnitType.Auto && value <= 0)
                {
                    value = 1;
                }

                _definitions[trackIndex] = new GridTrackDefinition(unit, value);
                RebuildRows();
                Commit();
            };
            void CommitValue()
            {
                if (!double.TryParse(
                        valueEntry.Text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value) ||
                    !double.IsFinite(value) ||
                    value < 0)
                {
                    _context.ShowError(
                        $"{_context.Property.Name} values must be non-negative numbers.");
                    return;
                }

                _definitions[trackIndex] = _definitions[trackIndex] with { Value = value };
                Commit();
            }

            valueEntry.Completed += (_, _) => CommitValue();
            valueEntry.Unfocused += (_, _) => CommitValue();
            removeButton.Clicked += (_, _) => RemoveTrack(trackIndex);
            _trackList.Children.Add(row);
        }
    }

    private void Commit() =>
        _context.Commit(_definitions.Count == 0
            ? null
            : GridDefinitionSerializer.Serialize(_definitions));
}
