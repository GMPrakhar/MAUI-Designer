using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner
{
    internal static class ElementCreator
    {
        internal static View Create(string elementTypeName)
        {
            // Get the class that extends View with the name as elementType
            var elementType = typeof(View).Assembly.GetTypes().FirstOrDefault(t => t.Name == elementTypeName);
            var newElement = Activator.CreateInstance(elementType) as View;
            newElement.Margin = new Thickness(20);
            if(newElement is Label)
            {
                (newElement as Label).Text = "Drag me!";
            }
            else if(newElement is Editor)
            {
                var element = newElement as Editor;
                element.Text = "Type here";
                // disable editor text edit
                element.IsEnabled = false;
            }

            return newElement;
        }
    }
}
