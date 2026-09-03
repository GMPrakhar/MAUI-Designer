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
}
