using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.XamlHelpers
{
    public class ColorXamlGenerator
    {
        public static string GenerateXamlForColor(string parentName, string colorHex)
        {
            return $"<{parentName} BackgroundColor=\"{colorHex}\" />";
        }
    }
}
