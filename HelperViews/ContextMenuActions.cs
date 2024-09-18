using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.HelperViews
{
    internal static class ContextMenuActions
    {
        public static void DetachFromParent_Clicked(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement?.Parent is Layout parentLayout)
            {
                // Remove the target element from its parent
                parentLayout.Children.Remove(targetElement);
                contextMenu.Close();
                Debug.WriteLine("DetachFromparent: Detached Focused view Parent");
            }
        }

        public static void LockInPlace_Clicked(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement != null)
            {
                // Disable all gesture recognizers to lock the element in place
                //targetElement.GestureRecognizers.Clear();
                contextMenu.Close();
                Debug.WriteLine("LockInPlace: Locked View in place");
            }
        }

        public static void BringToFrontButton_Clicked(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement?.Parent is Layout parentLayout)
            {
                // Remove the target element from its parent
                parentLayout.Children.Remove(targetElement);

                // Add the target element at the end of the parent's children collection
                parentLayout.Children.Add(targetElement);
                contextMenu.Close();
                Debug.WriteLine("BringToFrontButton: Moved the focused view to the Front.");
            }
        }

        public static void SendToBackButton_Clicked(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement?.Parent is Layout parentLayout)
            {
                // Remove the target element from its parent
                parentLayout.Children.Remove(targetElement);

                // Insert the target element at the beginning of the parent's children collection
                parentLayout.Children.Insert(0, targetElement);
                contextMenu.Close();
                Debug.WriteLine("SendToBack: Moved the focused view to the back.");
            }
        }
    }
}
