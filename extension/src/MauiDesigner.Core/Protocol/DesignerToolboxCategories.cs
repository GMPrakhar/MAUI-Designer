using System;

namespace MauiDesigner.Core.Protocol
{
    public static class DesignerToolboxCategories
    {
        public static string Normalize(string? category) =>
            string.IsNullOrWhiteSpace(category) ? "Other" : category!.Trim();

        public static string TabName(string? category) =>
            "MAUI - " + Normalize(category);

        public static int Order(string? category)
        {
            switch (Normalize(category))
            {
                case "Layouts":
                    return 0;
                case "Input":
                    return 1;
                case "Display":
                    return 2;
                case "Data and collections":
                    return 3;
                default:
                    return 4;
            }
        }
    }
}
