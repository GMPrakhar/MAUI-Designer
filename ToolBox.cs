using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner
{
    internal class ToolBox
    {
        internal static IDictionary<string, Type> GetAllVisualElementsAlongWithType()
        {
            var visualElements = typeof(Microsoft.Maui.Controls.View).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Microsoft.Maui.Controls.View)));
            var visualElementsWithType = new Dictionary<string, Type>();
            foreach (var visualElement in visualElements)
            {
                visualElementsWithType[visualElement.Name] = visualElement;
            }

            return visualElementsWithType;
        }
    }
}
