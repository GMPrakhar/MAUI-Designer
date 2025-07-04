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

        private static View CloneView(View originalView)
        {
            var newView = ElementCreator.Create(originalView.GetType().Name);

            newView.BindingContext = originalView.BindingContext;
            newView.Style = originalView.Style;

            return newView;
        }
    }
}
