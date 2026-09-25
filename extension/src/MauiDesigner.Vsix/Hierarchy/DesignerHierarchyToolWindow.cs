using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MauiDesigner.Core.Protocol;

using Microsoft.VisualStudio.Shell;

namespace MauiDesigner.Vsix
{
    [Guid(WindowGuidString)]
    public sealed class DesignerHierarchyToolWindow : ToolWindowPane
    {
        public const string WindowGuidString = "7bcf03ad-10a4-4d79-b40b-99ca7b386ce7";
        private readonly DesignerHierarchyControl _control;

        public DesignerHierarchyToolWindow() : base(null)
        {
            Caption = "MAUI Designer Hierarchy";
            _control = new DesignerHierarchyControl();
            Content = _control;
        }

        internal void SetOwner(DesignerPane? owner) => _control.SetOwner(owner);

        internal void UpdateItems(IReadOnlyList<DesignerHierarchyItem> items) =>
            _control.UpdateItems(items);
    }

    internal sealed class DesignerHierarchyControl : UserControl
    {
        private const string DragFormat = "MauiDesigner.HierarchyElement";
        private readonly ListBox _items;
        private DesignerPane? _owner;
        private bool _updating;
        private Point _dragStart;

        internal DesignerHierarchyControl()
        {
            var root = new DockPanel();
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4)
            };
            toolbar.Children.Add(ActionButton("Up", "Move selected element up", "moveUp"));
            toolbar.Children.Add(ActionButton("Down", "Move selected element down", "moveDown"));
            toolbar.Children.Add(ActionButton("Delete", "Delete selected element", "delete"));
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);

            _items = new ListBox
            {
                BorderThickness = new Thickness(0),
                AllowDrop = true
            };
            _items.SelectionChanged += OnSelectionChanged;
            _items.PreviewKeyDown += OnKeyDown;
            _items.PreviewMouseLeftButtonDown += (_, args) => _dragStart = args.GetPosition(_items);
            _items.PreviewMouseMove += OnMouseMove;
            _items.DragOver += OnDragOver;
            _items.Drop += OnDrop;
            root.Children.Add(_items);
            Content = root;
        }

        internal void SetOwner(DesignerPane? owner) => _owner = owner;

        internal void UpdateItems(IReadOnlyList<DesignerHierarchyItem> items)
        {
            _updating = true;
            try
            {
                string? selectedId = items.FirstOrDefault(item => item.IsSelected)?.ElementId;
                _items.Items.Clear();
                foreach (DesignerHierarchyItem item in items)
                {
                    var row = new TextBlock
                    {
                        Text = item.DisplayName,
                        Margin = new Thickness(8 + (item.Depth * 16), 4, 4, 4),
                        FontWeight = item.IsSelected ? FontWeights.SemiBold : FontWeights.Normal,
                        ToolTip = $"{item.DisplayName} ({item.ChildCount} children)",
                        Tag = item
                    };
                    _items.Items.Add(row);
                    if (item.ElementId == selectedId)
                    {
                        _items.SelectedItem = row;
                    }
                }

                if (_items.SelectedItem is not null)
                {
                    _items.ScrollIntoView(_items.SelectedItem);
                }
            }
            finally
            {
                _updating = false;
            }
        }

        private Button ActionButton(string text, string tooltip, string command)
        {
            var button = new Button
            {
                Content = text,
                ToolTip = tooltip,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(8, 3, 8, 3)
            };
            button.Click += (_, _) => _owner?.PostDesignerCommand(command);
            return button;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_updating && SelectedItem() is DesignerHierarchyItem item)
            {
                _owner?.PostHierarchyCommand("selectElement", item.ElementId);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                _owner?.PostDesignerCommand("delete");
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed ||
                SelectedItem() is not DesignerHierarchyItem item ||
                item.ParentElementId is null)
            {
                return;
            }

            Point current = e.GetPosition(_items);
            if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(
                _items,
                new DataObject(DragFormat, item.ElementId),
                DragDropEffects.Move);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DragFormat) && DropTarget(e) is not null
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DragFormat) is string sourceId &&
                DropTarget(e) is DesignerHierarchyItem target &&
                sourceId != target.ElementId)
            {
                _owner?.PostHierarchyCommand(
                    "reparentElement",
                    sourceId,
                    target.ElementId);
            }

            e.Handled = true;
        }

        private DesignerHierarchyItem? DropTarget(DragEventArgs e)
        {
            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current is not null && current is not ListBoxItem)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            return current is ListBoxItem container &&
                container.Content is TextBlock text
                ? text.Tag as DesignerHierarchyItem
                : null;
        }

        private DesignerHierarchyItem? SelectedItem() =>
            (_items.SelectedItem as TextBlock)?.Tag as DesignerHierarchyItem;
    }
}
