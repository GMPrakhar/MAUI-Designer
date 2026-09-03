using MAUIDesigner.Fresh.App.Catalog;

namespace MAUIDesigner.Fresh.App.PropertyEditing;

public sealed class PropertyEditorRegistry
{
    private readonly List<IPropertyEditor> _editors =
    [
        new BooleanPropertyEditor(),
        new EnumPropertyEditor(),
        new ThicknessPropertyEditor(),
        new GridLengthPropertyEditor(),
        new ColorPropertyEditor(),
        new NumericPropertyEditor(),
        new TextPropertyEditor()
    ];

    public void Register(IPropertyEditor editor, bool first = true)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (first)
        {
            _editors.Insert(0, editor);
        }
        else
        {
            _editors.Add(editor);
        }
    }

    public bool TryCreate(PropertyEditorContext context, out View? editor)
    {
        IPropertyEditor? factory = _editors.FirstOrDefault(candidate => candidate.CanEdit(context.Property));
        editor = factory?.Create(context);
        return editor is not null;
    }

    private sealed class BooleanPropertyEditor : IPropertyEditor
    {
        public bool CanEdit(PropertyDescriptor property) =>
            (Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType) == typeof(bool);

        public View Create(PropertyEditorContext context)
        {
            var editor = new Switch
            {
                AutomationId = AutomationId(context),
                HorizontalOptions = LayoutOptions.Start,
                IsToggled = bool.TryParse(context.Value, out bool value) && value
            };
            editor.Toggled += (_, args) => context.Commit(args.Value ? "True" : "False");
            return editor;
        }
    }

    private sealed class EnumPropertyEditor : IPropertyEditor
    {
        public bool CanEdit(PropertyDescriptor property) =>
            (Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType).IsEnum;

        public View Create(PropertyEditorContext context)
        {
            Type enumType = Nullable.GetUnderlyingType(context.Property.ValueType) ?? context.Property.ValueType;
            string[] names = Enum.GetNames(enumType);
            var editor = new Picker
            {
                AutomationId = AutomationId(context),
                FontSize = 11,
                HeightRequest = 36,
                ItemsSource = names
            };
            editor.SelectedIndex = Math.Max(0, Array.IndexOf(names, context.Value));
            editor.SelectedIndexChanged += (_, _) =>
            {
                if (editor.SelectedItem is string value)
                {
                    context.Commit(value);
                }
            };
            return editor;
        }
    }

    private sealed class ThicknessPropertyEditor : IPropertyEditor
    {
        public bool CanEdit(PropertyDescriptor property) =>
            (Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType) == typeof(Thickness);

        public View Create(PropertyEditorContext context)
        {
            double[] values = ParseThickness(context.Value);
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 5
            };
            string[] labels = ["L", "T", "R", "B"];
            var entries = new Entry[4];
            for (int index = 0; index < entries.Length; index++)
            {
                var entry = new Entry
                {
                    AutomationId = $"{AutomationId(context)}-{labels[index].ToLowerInvariant()}",
                    FontSize = 10,
                    HeightRequest = 34,
                    Keyboard = Keyboard.Numeric,
                    Placeholder = labels[index],
                    Text = values[index].ToString(System.Globalization.CultureInfo.InvariantCulture)
                };
                entries[index] = entry;
                grid.Add(entry, index);
            }

            void Commit()
            {
                if (entries.Any(entry =>
                        !double.TryParse(
                            entry.Text,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out _)))
                {
                    context.ShowError($"{context.Property.Name} requires four numbers.");
                    return;
                }

                context.Commit(string.Join(",", entries.Select(entry => entry.Text)));
            }

            foreach (Entry entry in entries)
            {
                entry.Completed += (_, _) => Commit();
                entry.Unfocused += (_, _) => Commit();
            }

            return grid;
        }

        private static double[] ParseThickness(string? text)
        {
            double[] parsed = (text ?? "0")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(value => double.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double number) ? number : 0)
                .ToArray();
            return parsed.Length switch
            {
                1 => [parsed[0], parsed[0], parsed[0], parsed[0]],
                2 => [parsed[0], parsed[1], parsed[0], parsed[1]],
                4 => parsed,
                _ => [0, 0, 0, 0]
            };
        }
    }

    private sealed class GridLengthPropertyEditor : IPropertyEditor
    {
        public bool CanEdit(PropertyDescriptor property) =>
            (Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType) == typeof(GridLength);

        public View Create(PropertyEditorContext context) =>
            CreateEntry(context, "Auto, *, 2*, or pixels", Keyboard.Default);
    }

    private sealed class ColorPropertyEditor : IPropertyEditor
    {
        public bool CanEdit(PropertyDescriptor property)
        {
            Type type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;
            return type == typeof(Color) || typeof(Brush).IsAssignableFrom(type);
        }

        public View Create(PropertyEditorContext context)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(24)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            var swatch = new Border
            {
                WidthRequest = 22,
                HeightRequest = 22,
                VerticalOptions = LayoutOptions.Center,
                Stroke = Color.FromArgb("#4B5266"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = new CornerRadius(5)
                }
            };
            if (Color.TryParse(context.Value, out Color? color))
            {
                swatch.BackgroundColor = color;
            }

            Entry entry = CreateEntry(context, "#RRGGBB or named color", Keyboard.Default);
            grid.Add(swatch);
            grid.Add(entry, 1);
            return grid;
        }
    }

    private sealed class NumericPropertyEditor : IPropertyEditor
    {
        private static readonly HashSet<Type> NumericTypes =
        [
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        ];

        public bool CanEdit(PropertyDescriptor property) =>
            NumericTypes.Contains(Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType);

        public View Create(PropertyEditorContext context) =>
            CreateEntry(context, context.Property.ValueType.Name, Keyboard.Numeric);
    }

    private sealed class TextPropertyEditor : IPropertyEditor
    {
        public bool CanEdit(PropertyDescriptor property)
        {
            Type type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;
            return type == typeof(string) ||
                System.ComponentModel.TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
        }

        public View Create(PropertyEditorContext context) =>
            CreateEntry(context, context.Property.ValueType.Name, Keyboard.Default);
    }

    private static Entry CreateEntry(
        PropertyEditorContext context,
        string placeholder,
        Keyboard keyboard)
    {
        var editor = new Entry
        {
            AutomationId = AutomationId(context),
            FontSize = 11,
            HeightRequest = 34,
            Keyboard = keyboard,
            Placeholder = placeholder,
            Text = context.Value ?? string.Empty
        };
        void Commit() => context.Commit(string.IsNullOrWhiteSpace(editor.Text) ? null : editor.Text);
        editor.Completed += (_, _) => Commit();
        editor.Unfocused += (_, _) => Commit();
        return editor;
    }

    private static string AutomationId(PropertyEditorContext context) =>
        $"property-{context.Property.Name}";
}
