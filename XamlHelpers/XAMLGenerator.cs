﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.XamlHelpers
{
    class XAMLGenerator
    {
        private static Type[] allowedTypesForXamlProperties = { typeof(string), typeof(Color), typeof(Thickness), typeof(Enum) };

        public static string GetXamlForElement(VisualElement element)
        {
            var xaml = GetInternalXAML(element);
            var finalXamlBuilder = new StringBuilder();
            finalXamlBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>");
            finalXamlBuilder.AppendLine(xaml);
            finalXamlBuilder.AppendLine("</ContentPage>");

            return finalXamlBuilder.ToString();
        }

        private static string GetInternalXAML(VisualElement element)
        {
            StringBuilder xamlBuilder = new StringBuilder();
            if (element.StyleId == Constants.DraggingViewName)
            {
                return string.Empty;
            }

            xamlBuilder.AppendLine($"<{element.GetType().Name}");

            foreach (var property in element.GetType().GetProperties())
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    var value = property.GetValue(element);
                    var valueType = value?.GetType();
                    if (value != null && IsSupportedType(valueType) && value.ToString() != "∞")
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

                        xamlBuilder.AppendLine($"    {property.Name}=\"{value}\"");
                    }
                }
            }

            // Check if element can contain children
            if (element is Layout layout)
            {
                xamlBuilder.AppendLine(">");
                foreach (var child in layout.Children.Where(x => x is VisualElement))
                {
                    xamlBuilder.AppendLine(GetInternalXAML(child as VisualElement));
                }
                xamlBuilder.AppendLine($"</{element.GetType().Name}>");
            }
            else
            {

                xamlBuilder.AppendLine("/>");
            }
            return xamlBuilder.ToString();
        }

        private static bool IsSupportedType(Type valueType)
        {
            // Check if the type is a primitive type or string
            return valueType.IsPrimitive || valueType.IsEnum || allowedTypesForXamlProperties.Contains(valueType);
        }
    }
}