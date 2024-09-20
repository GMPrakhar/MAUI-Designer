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
        public static View? ClipboardElement { get; set; }
        public static void DetachFromParent(View targetElement, ContextMenu contextMenu, EventArgs e, AbsoluteLayout designerFrame)
        {
            if (targetElement?.Parent is Layout parentLayout)
            {
                // Remove the target element from its parent
                parentLayout.Children.Remove(targetElement);
                designerFrame.Children.Add(targetElement);
                contextMenu.Close();
                Debug.WriteLine("DetachFromparent: Detached Focused view Parent");
            }
        }

        public static void LockInPlace(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement != null)
            {
                // Disable all gesture recognizers to lock the element in place
                //targetElement.GestureRecognizers.Clear();
                contextMenu.Close();
                Debug.WriteLine("LockInPlace: Locked View in place");
            }
        }

        public static void BringToFrontButton(View targetElement, ContextMenu contextMenu, EventArgs e)
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

        public static void SendToBackButton(View targetElement, ContextMenu contextMenu, EventArgs e)
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

        public static void CutElement(View targetElement, ContextMenu contextMenu, EventArgs e, AbsoluteLayout designerFrame)
        {
            if (targetElement != null)
            {
                ClipboardElement = targetElement;
                designerFrame.Children.Remove(targetElement);
                contextMenu.Close();
            }
        }


        public static void CopyElement(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement != null)
            {
                ClipboardElement = targetElement;
                contextMenu.Close();
            }
        }

        public static void PasteElement(View targetElement, ContextMenu contextMenu, EventArgs e, AbsoluteLayout designerFrame)
        {
            if (ClipboardElement != null)
            {
                var newElement = CloneView(ClipboardElement);
                designerFrame.Children.Add(newElement);
                ClipboardElement = null;

                //TODO: fix to paste where the pointer is clicked
            }
            contextMenu.Close();
        }

        private static View CloneView(View originalView)
        {
            
            var newView = ElementCreator.Create(originalView.GetType().Name);

            newView.BindingContext = originalView.BindingContext;
            newView.Style = originalView.Style;

            return newView;
        }

        public static void DeleteElement(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement != null)
            {
                // Remove the focused view from its parent layout
                (targetElement.Parent as Layout)?.Remove(targetElement);
            }
            contextMenu.Close();
        }
    }
}
