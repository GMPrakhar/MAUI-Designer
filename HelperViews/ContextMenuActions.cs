using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUIDesigner.HelperViews
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ContextMenuActionAttribute : Attribute
    {
        public string DisplayName { get; }
        public ContextMenuActionAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }

    internal static class ContextMenuActions
    {
        public static View? ClipboardElement { get; set; }

        [ContextMenuAction("Detach from Parent")]
        public static void DetachFromParent(View targetElement, ContextMenu contextMenu, EventArgs e, Layout designerFrame)
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

        [ContextMenuAction("Lock in Place")]
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

        [ContextMenuAction("Bring to Front")]
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

        [ContextMenuAction("Send to Back")]
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

        [ContextMenuAction("Cut Element")]
        public static void CutElement(View targetElement, ContextMenu contextMenu, EventArgs e, Layout designerFrame)
        {
            if (targetElement != null)
            {
                ClipboardElement = targetElement;
                designerFrame.Children.Remove(targetElement);
                contextMenu.Close();
            }
        }

        [ContextMenuAction("Copy Element")]
        public static void CopyElement(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement != null)
            {
                ClipboardElement = targetElement;
                contextMenu.Close();
            }
        }

        [ContextMenuAction("Paste Element")]
        public static void PasteElement(View targetElement, ContextMenu contextMenu, EventArgs e, Layout designerFrame)
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

        [ContextMenuAction("Delete Element")]
        public static void DeleteElement(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            if (targetElement != null)
            {
                // Remove the focused view from its parent layout
                (targetElement.Parent as Layout)?.Remove(targetElement);
            }
            contextMenu.Close();
        }

        [ContextMenuAction("Undo")]
        public static void Undo(View targetElement, ContextMenu contextMenu, EventArgs e)
        {

        }

        [ContextMenuAction("Redo")]
        public static void Redo(View targetElement, ContextMenu contextMenu, EventArgs e)
        {

        }
    }
}
