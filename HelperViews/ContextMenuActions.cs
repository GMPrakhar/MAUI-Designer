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
        public static Core.Elements.IElementOperations ElementOperations { get; set; }

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

        [ContextMenuAction("Add Column")]
        public static void AddColumn(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            var grid = GetGridFromDesignerView(targetElement);
            if (grid != null)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                contextMenu.Close();
            }
        }

        [ContextMenuAction("Remove Column")]
        public static void RemoveColumn(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            var grid = GetGridFromDesignerView(targetElement);
            if (grid != null && grid.ColumnDefinitions.Count > 1)
            {
                grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
                contextMenu.Close();
            }
        }

        [ContextMenuAction("Add Row")]
        public static void AddRow(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            var grid = GetGridFromDesignerView(targetElement);
            if (grid != null)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                contextMenu.Close();
            }
        }

        [ContextMenuAction("Remove Row")]
        public static void RemoveRow(View targetElement, ContextMenu contextMenu, EventArgs e)
        {
            var grid = GetGridFromDesignerView(targetElement);
            if (grid != null && grid.RowDefinitions.Count > 1)
            {
                grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
                contextMenu.Close();
            }
        }

        private static Grid? GetGridFromDesignerView(View targetElement)
        {
            if (targetElement is ElementDesignerView edv && edv.View is Grid grid)
                return grid;
            if (targetElement is Grid directGrid)
                return directGrid;
            return null;
        }

        private static View CloneView(View originalView)
        {
            // Try to use ElementOperations if available, otherwise fall back to ElementCreator
            View newView;
            if (ElementOperations is Core.Elements.ElementService elementService)
            {
                try
                {
                    newView = elementService.CreateElement(originalView.GetType().Name);
                }
                catch
                {
                    // Fall back to ElementCreator if the element type is not supported
                    newView = ElementCreator.Create(originalView.GetType().Name);
                }
            }
            else
            {
                newView = ElementCreator.Create(originalView.GetType().Name);
            }

            newView.BindingContext = originalView.BindingContext;
            newView.Style = originalView.Style;

            return newView;
        }
    }
}
