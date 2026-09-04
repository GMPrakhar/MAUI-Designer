using System.Reflection;
using System.Text.Json;

namespace MAUIDesigner
{
    internal static class IconMappingLoader
    {
        public static IDictionary<string, string> Load()
        {
            try
            {
                var assembly = typeof(IconMappingLoader).Assembly;
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("iconMapping.json", StringComparison.OrdinalIgnoreCase));
                if (resourceName != null)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
                        if (map != null)
                        {
                            map.TryAdd("Default", "\ue724");
                            return map;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Icon mapping load failed: {ex.Message}");
            }

            return new Dictionary<string, string> { ["Default"] = "\ue724" };
        }
    }
}
