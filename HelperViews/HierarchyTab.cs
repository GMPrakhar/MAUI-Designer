using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;

namespace MAUIDesigner.HelperViews
{
    public class HierarchyTab : TabMenu
    {
        private ScrollView scrollView;
        private VerticalStackLayout hierarchyLayout;
        private AbsoluteLayout? designerFrame;
        private ElementDesignerView? currentlyFocusedElement;
        private Dictionary<View, bool> expandedStates = new();

        public HierarchyTab()
        {
            TabName = "Hierarchy";
            hierarchyLayout = new VerticalStackLayout 
            { 
                Spacing = 2, 
                Padding = new Thickness(5) 
            };
            
            scrollView = new ScrollView
            {
                Content = hierarchyLayout
            };
            
            TabContent = scrollView;
        }

        public void SetDesignerFrame(AbsoluteLayout designerFrame)
        {
            this.designerFrame = designerFrame;
            
            // Subscribe to focus changes to highlight the current element
            MAUIDesigner.DnDHelper.DragAndDropOperations.OnFocusChanged += OnFocusChanged;
            
            UpdateHierarchy();
        }

        private void OnFocusChanged(object obj)
        {
            if (obj is ElementDesignerView elementDesigner)
            {
                currentlyFocusedElement = elementDesigner;
                UpdateHierarchy(); // Refresh to show current selection
            }
            else
            {
                currentlyFocusedElement = null;
                UpdateHierarchy();
            }
        }

        public void UpdateHierarchy()
        {
            if (designerFrame == null) return;

            hierarchyLayout.Children.Clear();

            // Get all ElementDesignerView instances from the designer frame
            var designerViews = designerFrame.Children
                .OfType<ElementDesignerView>()
                .Where(dv => dv.View != null)
                .ToList();

            if (designerViews.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "No elements in designer",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    Padding = new Thickness(15, 20),
                    HorizontalTextAlignment = TextAlignment.Center
                };
                hierarchyLayout.Children.Add(emptyLabel);
                return;
            }

            // Create a hierarchical structure - start with top-level elements
            foreach (var designerView in designerViews.OrderBy(dv => GetElementDisplayName(dv)))
            {
                var hierarchyItem = CreateHierarchyItem(designerView, 0);
                hierarchyLayout.Children.Add(hierarchyItem);
                
                // Add children if this is a layout and is expanded
                if (designerView.View is Layout layout)
                {
                    AddChildrenToHierarchy(designerView, layout, 1);
                }
            }
        }

        private void AddChildrenToHierarchy(ElementDesignerView parentDesignerView, Layout layout, int indentLevel)
        {
            if (parentDesignerView != null && !IsExpanded(parentDesignerView.View))
                return;

            foreach (var child in layout.Children)
            {
                // Check if the child is an ElementDesignerView
                if (child is ElementDesignerView childDesignerView)
                {
                    var childHierarchyItem = CreateHierarchyItem(childDesignerView, indentLevel);
                    hierarchyLayout.Children.Add(childHierarchyItem);
                    
                    // Recursively add children if this child is also a layout
                    if (childDesignerView.View is Layout childLayout)
                    {
                        AddChildrenToHierarchy(childDesignerView, childLayout, indentLevel + 1);
                    }
                }
                else
                {
                    // If the child is not wrapped in ElementDesignerView, create a simple item
                    var childItem = CreateSimpleHierarchyItem(child, indentLevel);
                    hierarchyLayout.Children.Add(childItem);
                    
                    // If this child is a layout, add its children too
                    if (child is Layout childLayout)
                    {
                        AddChildrenToHierarchy(null, childLayout, indentLevel + 1);
                    }
                }
            }
        }

        private View CreateSimpleHierarchyItem(View view, int indentLevel)
        {
            var elementName = GetSimpleElementDisplayName(view);
            
            var itemLayout = new HorizontalStackLayout
            {
                Spacing = 5,
                Padding = new Thickness(5 + (indentLevel * 15), 2, 5, 2)
            };

            // Add spacer for non-expandable items
            var spacer = new Label
            {
                Text = " ",
                WidthRequest = 15
            };
            itemLayout.Children.Add(spacer);

            // Add element icon/indicator
            var indicator = new Label
            {
                Text = GetSimpleElementIcon(view),
                FontSize = 12,
                TextColor = Colors.LightBlue,
                VerticalTextAlignment = TextAlignment.Center,
                WidthRequest = 20,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var nameLabel = new Label
            {
                Text = elementName,
                FontSize = 12,
                TextColor = Colors.White,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Start
            };

            itemLayout.Children.Add(indicator);
            itemLayout.Children.Add(nameLabel);

            // Style the item
            var itemFrame = new Border
            {
                BackgroundColor = Colors.Transparent,
                Content = itemLayout,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 1),
                StrokeThickness = 0
            };

            return itemFrame;
        }

        private string GetSimpleElementDisplayName(View view)
        {
            if (view == null) return "Unknown";

            var elementType = view.GetType().Name;
            
            // Try to get more specific information if available
            var displayName = elementType;
            
            // For common controls, try to get more descriptive information
            switch (view)
            {
                case Label label when !string.IsNullOrEmpty(label.Text):
                    displayName = $"{elementType} (\"{TruncateText(label.Text)}\")";
                    break;
                case Button button when !string.IsNullOrEmpty(button.Text):
                    displayName = $"{elementType} (\"{TruncateText(button.Text)}\")";
                    break;
                case Entry entry when !string.IsNullOrEmpty(entry.Text):
                    displayName = $"{elementType} (\"{TruncateText(entry.Text)}\")";
                    break;
                case Editor editor when !string.IsNullOrEmpty(editor.Text):
                    displayName = $"{elementType} (\"{TruncateText(editor.Text)}\")";
                    break;
                case BoxView boxView:
                    displayName = $"{elementType} ({boxView.BackgroundColor})";
                    break;
                case Image image when !string.IsNullOrEmpty(image.Source?.ToString()):
                    displayName = $"{elementType} ({TruncateText(image.Source.ToString())})";
                    break;
                default:
                    // For layout elements, show child count if available
                    if (view is Layout layout)
                    {
                        displayName = $"{elementType} ({layout.Children.Count} items)";
                    }
                    else
                    {
                        displayName = elementType;
                    }
                    break;
            }

            return displayName;
        }

        private string GetSimpleElementIcon(View view)
        {
            if (view == null) return "🔹";

            return view switch
            {
                Label => "🏷️",
                Button => "🔘",
                Entry => "📝",
                Editor => "📄",
                Image => "🖼️",
                BoxView => "⬜",
                Grid => "📊",
                StackLayout => "📋",
                HorizontalStackLayout => "↔️",
                VerticalStackLayout => "↕️",
                AbsoluteLayout => "🎯",
                ScrollView => "📜",
                Border => "⬛",
                Frame => "🖼️",
                ContentView => "📦",
                CollectionView => "📝",
                ListView => "📑",
                _ => "🔹"
            };
        }

        private bool IsExpanded(View view)
        {
            return expandedStates.ContainsKey(view) && expandedStates[view];
        }

        private void ToggleExpanded(View view)
        {
            if (expandedStates.ContainsKey(view))
            {
                expandedStates[view] = !expandedStates[view];
            }
            else
            {
                expandedStates[view] = true;
            }
            UpdateHierarchy();
        }

        private View CreateHierarchyItem(ElementDesignerView designerView, int indentLevel)
        {
            var elementName = GetElementDisplayName(designerView);
            var isCurrentlyFocused = currentlyFocusedElement == designerView;
            var isLayout = designerView.View is Layout;
            var layoutHasChildren = isLayout && ((Layout)designerView.View).Children.Count > 0;

            var itemLayout = new HorizontalStackLayout
            {
                Spacing = 5,
                Padding = new Thickness(5 + (indentLevel * 15), 2, 5, 2)
            };

            // Add expand/collapse button for layouts with children
            if (layoutHasChildren)
            {
                var expandIcon = new Label
                {
                    Text = IsExpanded(designerView.View) ? "▼" : "▶",
                    FontSize = 10,
                    TextColor = Colors.LightGray,
                    VerticalTextAlignment = TextAlignment.Center,
                    WidthRequest = 15,
                    HorizontalTextAlignment = TextAlignment.Center
                };

                var expandTapGesture = new TapGestureRecognizer();
                expandTapGesture.Tapped += (s, e) =>
                {
                    ToggleExpanded(designerView.View);
                };
                expandIcon.GestureRecognizers.Add(expandTapGesture);
                itemLayout.Children.Add(expandIcon);
            }
            else
            {
                // Add spacer for non-expandable items
                var spacer = new Label
                {
                    Text = " ",
                    WidthRequest = 15
                };
                itemLayout.Children.Add(spacer);
            }

            // Add element icon/indicator
            var indicator = new Label
            {
                Text = GetElementIcon(designerView),
                FontSize = 12,
                TextColor = isCurrentlyFocused ? Colors.Yellow : Colors.LightBlue,
                VerticalTextAlignment = TextAlignment.Center,
                WidthRequest = 20,
                HorizontalTextAlignment = TextAlignment.Center
            };

            var nameLabel = new Label
            {
                Text = elementName,
                FontSize = 12,
                TextColor = isCurrentlyFocused ? Colors.Yellow : Colors.White,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Start,
                FontAttributes = isCurrentlyFocused ? FontAttributes.Bold : FontAttributes.None
            };

            itemLayout.Children.Add(indicator);
            itemLayout.Children.Add(nameLabel);

            // Add tap gesture to highlight the element
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => HighlightElement(designerView);
            itemLayout.GestureRecognizers.Add(tapGesture);

            // Add right-click context menu
            var rightClickGesture = new TapGestureRecognizer();
            rightClickGesture.Buttons = ButtonsMask.Secondary;
            rightClickGesture.Tapped += (s, e) => ShowContextMenu(designerView, e);
            itemLayout.GestureRecognizers.Add(rightClickGesture);

            // Style the item
            var itemFrame = new Border
            {
                BackgroundColor = isCurrentlyFocused ? Color.FromArgb("#44666600") : Colors.Transparent,
                Content = itemLayout,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 1),
                StrokeThickness = 0
            };

            // Add hover effect
            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += (s, e) => 
            {
                if (!isCurrentlyFocused)
                    itemFrame.BackgroundColor = Color.FromArgb("#33444444");
            };
            pointerGesture.PointerExited += (s, e) => 
            {
                if (!isCurrentlyFocused)
                    itemFrame.BackgroundColor = Colors.Transparent;
            };
            itemFrame.GestureRecognizers.Add(pointerGesture);

            return itemFrame;
        }

        private string GetElementDisplayName(ElementDesignerView designerView)
        {
            if (designerView?.View == null) return "Unknown";

            var elementType = designerView.View.GetType().Name;
            
            // Try to get more specific information if available
            var displayName = elementType;
            
            // For common controls, try to get more descriptive information
            switch (designerView.View)
            {
                case Label label when !string.IsNullOrEmpty(label.Text):
                    displayName = $"{elementType} (\"{TruncateText(label.Text)}\")";
                    break;
                case Button button when !string.IsNullOrEmpty(button.Text):
                    displayName = $"{elementType} (\"{TruncateText(button.Text)}\")";
                    break;
                case Entry entry when !string.IsNullOrEmpty(entry.Text):
                    displayName = $"{elementType} (\"{TruncateText(entry.Text)}\")";
                    break;
                case Editor editor when !string.IsNullOrEmpty(editor.Text):
                    displayName = $"{elementType} (\"{TruncateText(editor.Text)}\")";
                    break;
                case BoxView boxView:
                    displayName = $"{elementType} ({boxView.BackgroundColor})";
                    break;
                case Image image when !string.IsNullOrEmpty(image.Source?.ToString()):
                    displayName = $"{elementType} ({TruncateText(image.Source.ToString())})";
                    break;
                default:
                    // For layout elements, show child count if available
                    if (designerView.View is Layout layout)
                    {
                        displayName = $"{elementType} ({layout.Children.Count} items)";
                    }
                    else
                    {
                        displayName = elementType;
                    }
                    break;
            }

            return displayName;
        }

        private string TruncateText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length > 20 ? text.Substring(0, 20) + "..." : text;
        }

        private string GetElementIcon(ElementDesignerView designerView)
        {
            if (designerView?.View == null) return "🔹";

            return designerView.View switch
            {
                Label => "🏷️",
                Button => "🔘",
                Entry => "📝",
                Editor => "📄",
                Image => "🖼️",
                BoxView => "⬜",
                Grid => "📊",
                StackLayout => "📋",
                HorizontalStackLayout => "↔️",
                VerticalStackLayout => "↕️",
                AbsoluteLayout => "🎯",
                ScrollView => "📜",
                Border => "⬛",
                Frame => "🖼️",
                ContentView => "📦",
                CollectionView => "📝",
                ListView => "📑",
                _ => "🔹"
            };
        }

        private void ShowContextMenu(ElementDesignerView designerView, TappedEventArgs e)
        {
            // First focus the element
            HighlightElement(designerView);
            
            // Create a mock TappedEventArgs with the position relative to the designer frame
            // For now, we'll position the context menu at the top of the designer frame
            // In a real implementation, we'd want to position it relative to the hierarchy item
            var mockArgs = new TappedEventArgs(new Point(100, 100));
            
            // Show the context menu using the existing ToolBox.ShowContextMenu method
            ToolBox.ShowContextMenu(designerFrame, mockArgs);
        }

        private void HighlightElement(ElementDesignerView designerView)
        {
            // Use the existing focus system to highlight the element
            MAUIDesigner.DnDHelper.DragAndDropOperations.OnFocusChanged?.Invoke(designerView);
        }
    }
}