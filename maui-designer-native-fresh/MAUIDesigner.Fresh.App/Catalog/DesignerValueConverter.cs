using System.ComponentModel;
using System.Globalization;

namespace MAUIDesigner.Fresh.App.Catalog;

public static class DesignerValueConverter
{
    public static bool TryConvert(string text, Type targetType, out object? value)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType == typeof(string))
        {
            value = text;
            return true;
        }

        if (effectiveType.IsEnum && Enum.TryParse(effectiveType, text, ignoreCase: true, out object? parsed))
        {
            value = parsed;
            return true;
        }

        if (effectiveType == typeof(RowDefinitionCollection))
        {
            return TryCreateRows(text, out value);
        }

        if (effectiveType == typeof(ColumnDefinitionCollection))
        {
            return TryCreateColumns(text, out value);
        }

        TypeConverter converter = TypeDescriptor.GetConverter(effectiveType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                value = converter.ConvertFromInvariantString(text);
                return true;
            }
            catch (FormatException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        try
        {
            value = Convert.ChangeType(text, effectiveType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
        }
        catch (InvalidCastException)
        {
        }
        catch (OverflowException)
        {
        }

        value = null;
        return false;
    }

    private static bool TryCreateRows(string text, out object? value)
    {
        var rows = new RowDefinitionCollection();
        foreach (string token in SplitDefinitions(text))
        {
            if (!TryParseGridLength(token, out GridLength length))
            {
                value = null;
                return false;
            }

            rows.Add(new RowDefinition { Height = length });
        }

        value = rows;
        return true;
    }

    private static bool TryCreateColumns(string text, out object? value)
    {
        var columns = new ColumnDefinitionCollection();
        foreach (string token in SplitDefinitions(text))
        {
            if (!TryParseGridLength(token, out GridLength length))
            {
                value = null;
                return false;
            }

            columns.Add(new ColumnDefinition { Width = length });
        }

        value = columns;
        return true;
    }

    private static string[] SplitDefinitions(string text) =>
        text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool TryParseGridLength(string text, out GridLength value)
    {
        if (text.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            value = GridLength.Auto;
            return true;
        }

        if (text.EndsWith('*'))
        {
            string weightText = text[..^1];
            double weight = weightText.Length == 0
                ? 1
                : double.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                    ? parsed
                    : double.NaN;
            if (double.IsFinite(weight) && weight >= 0)
            {
                value = new GridLength(weight, GridUnitType.Star);
                return true;
            }
        }
        else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double absolute) &&
                 double.IsFinite(absolute) &&
                 absolute >= 0)
        {
            value = new GridLength(absolute);
            return true;
        }

        value = default;
        return false;
    }
}
