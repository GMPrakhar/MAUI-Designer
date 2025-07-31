using MAUIDesigner.HelperViews;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.XamlHelpers
{
    class XAMLGenerator
    {
        private static Type[] allowedTypesForXamlProperties = { typeof(string), typeof(Color), typeof(Thickness), typeof(Enum), typeof(ColumnDefinitionCollection), typeof(RowDefinitionCollection) };

        public static string GetXamlForElement(VisualElement element)
        {
            var xaml = GetInternalXAML(element);
            var finalXamlBuilder = new StringBuilder();
            finalXamlBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>");
            finalXamlBuilder.AppendLine(xaml);
            finalXamlBuilder.AppendLine("</ContentPage>");

            return finalXamlBuilder.ToString();
        }

        internal static string GetInternalXAML(VisualElement element)
        {
            StringBuilder xamlBuilder = new StringBuilder();
            if (Constants.FrameworkElementNames.Contains(element.StyleId))
            {
                return string.Empty;
            }
            ElementDesignerView designerView = element as ElementDesignerView;
            if(designerView != null)
            {
                element = designerView.View;
                (element as View).Margin = designerView.EncapsulatingViewProperty.Margin;
                (element as View).WidthRequest = designerView.EncapsulatingViewProperty.WidthRequest;
                (element as View).HeightRequest = designerView.EncapsulatingViewProperty.HeightRequest;
            }

            xamlBuilder.AppendLine($"<{element.GetType().Name}");

            var defaultElement = Activator.CreateInstance(element.GetType());

            var compoundPropertiesAsChild = new List<string>();

            foreach (var property in element.GetType().GetProperties())
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    var value = property.GetValue(element);
                    var defaultValue = property.GetValue(defaultElement);
                    var valueType = value?.GetType();

                    if (value != null && IsSupportedType(valueType) && value.ToString() != "∞" && (defaultValue == null || !defaultValue.Equals(value)))
                    {
                        if (valueType == typeof(Color))
                        {
                            value = ((Color)value).ToArgbHex(true);
                        }
                        else if (valueType == typeof(Thickness))
                        {
                            var tValue = ((Thickness)value);
                            value = $"{tValue.Left},{tValue.Top},{tValue.Right},{tValue.Bottom}";
                        }
                        else if (valueType == typeof( ColumnDefinitionCollection) || valueType == typeof(RowDefinitionCollection))
                        {
                            compoundPropertiesAsChild.Add(GetXamlForDefintionCollection(property.Name, value, valueType));
                            continue;
                        }

                        xamlBuilder.AppendLine($"    {property.Name}=\"{value}\"");
                    }
                }
            }

            if(designerView?.Parent is Grid parentGrid)
            {
                var row = parentGrid.GetRow(designerView);
                var column = parentGrid.GetColumn(designerView);
                var rowSpan = parentGrid.GetRowSpan(designerView);
                var columnSpan = parentGrid.GetColumnSpan(designerView);
                xamlBuilder.AppendLine($"    OwningGrid.Row=\"{row}\"");
                xamlBuilder.AppendLine($"    OwningGrid.Column=\"{column}\"");
                xamlBuilder.AppendLine($"    OwningGrid.RowSpan=\"{rowSpan}\"");
                xamlBuilder.AppendLine($"    OwningGrid.ColumnSpan=\"{columnSpan}\"");
            }

            (element as View).Margin = 0;
            (element as View).WidthRequest = -1;
            (element as View).HeightRequest = -1;

            // Check if element can contain children
            if (element is Layout layout)
            {
                xamlBuilder.AppendLine(">");
                foreach (var child in layout.Children.Where(x => x is VisualElement))
                {
                    xamlBuilder.AppendLine(GetInternalXAML(child as VisualElement));
                }

                foreach(var compoundProperty in compoundPropertiesAsChild)
                {
                    xamlBuilder.AppendLine(compoundProperty);
                }

                xamlBuilder.AppendLine($"</{element.GetType().Name}>");
            }
            else
            {

                xamlBuilder.AppendLine("/>");
            }
            return xamlBuilder.ToString();
        }

        private static string GetXamlForDefintionCollection(string propertyName, object? value, Type? valueType)
        {
            var stringBuilder = new StringBuilder();
            var sizeName = valueType == typeof(ColumnDefinitionCollection) ? "Width" : "Height";
            stringBuilder.AppendLine($"<OwningGrid.{propertyName}>");
            var gridDefinitions = value as IEnumerable;
            foreach (var definition in gridDefinitions)
            {
                var sizeValue = definition is ColumnDefinition column ? (column.Width.IsAbsolute ? column.Width.Value.ToString() : "*"): ((definition as RowDefinition).Height.IsAbsolute ? (definition as RowDefinition).Height.Value.ToString() : "*");
                stringBuilder.AppendLine($"<{valueType.Name.Replace("Collection", "")} {sizeName}=\"{sizeValue}\"/>");
            }

            stringBuilder.AppendLine($"</OwningGrid.{propertyName}>");
            return stringBuilder.ToString();
        }

        private static bool IsSupportedType(Type valueType)
        {
            // Check if the type is a primitive type or string
            return valueType.IsPrimitive || valueType.IsEnum || allowedTypesForXamlProperties.Contains(valueType);
        }
    }
}
