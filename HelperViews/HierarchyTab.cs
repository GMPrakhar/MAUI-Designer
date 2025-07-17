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

            // Group by element type for better organization
            var groupedElements = designerViews
                .GroupBy(dv => dv.View.GetType().Name)
                .OrderBy(g => g.Key);

            foreach (var group in groupedElements)
            {
                // Add group header
                var groupHeader = new Label
                {
                    Text = $"{group.Key} ({group.Count()})",
                    FontSize = 13,
                    TextColor = Colors.LightGray,
                    FontAttributes = FontAttributes.Bold,
                    Padding = new Thickness(5, 8, 5, 2),
                    BackgroundColor = Color.FromArgb("#22333333")
                };
                hierarchyLayout.Children.Add(groupHeader);

                // Add each element in the group
                foreach (var designerView in group.OrderBy(dv => GetElementDisplayName(dv)))
                {
                    var hierarchyItem = CreateHierarchyItem(designerView, 1);
                    hierarchyLayout.Children.Add(hierarchyItem);
                }
            }
        }

        private View CreateHierarchyItem(ElementDesignerView designerView, int indentLevel)
        {
            var elementName = GetElementDisplayName(designerView);
            var isCurrentlyFocused = currentlyFocusedElement == designerView;

            var itemLayout = new HorizontalStackLayout
            {
                Spacing = 5,
                Padding = new Thickness(15, 2, 5, 2)
            };

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

        private void HighlightElement(ElementDesignerView designerView)
        {
            // Use the existing focus system to highlight the element
            MAUIDesigner.DnDHelper.DragAndDropOperations.OnFocusChanged?.Invoke(designerView);
        }
    }
}