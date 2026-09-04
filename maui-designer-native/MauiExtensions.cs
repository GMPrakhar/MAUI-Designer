using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner
{
    public static class MauiExtensions
    {
        public static void ChangeCursor(this UIElement uiElement, InputCursor cursor)
        {
            Type type = typeof(UIElement);
            type.InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, uiElement, new object[] { cursor });
        }
    }
}
