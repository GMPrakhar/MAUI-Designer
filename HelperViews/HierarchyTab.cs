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
            UpdateHierarchy();
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

            // Add tap gesture to highlight the element
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => HighlightElement(designerView);
            itemLayout.GestureRecognizers.Add(tapGesture);

            // Style the item
            var itemFrame = new Border
            {
                BackgroundColor = Colors.Transparent,
                Content = itemLayout,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 1),
                StrokeThickness = 0
            };

            // Add hover effect
            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += (s, e) => itemFrame.BackgroundColor = Color.FromArgb("#33444444");
            pointerGesture.PointerExited += (s, e) => itemFrame.BackgroundColor = Colors.Transparent;
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
                    displayName = $"{elementType} (\"{label.Text.Substring(0, Math.Min(label.Text.Length, 15))}{(label.Text.Length > 15 ? "..." : "")}\")";
                    break;
                case Button button when !string.IsNullOrEmpty(button.Text):
                    displayName = $"{elementType} (\"{button.Text.Substring(0, Math.Min(button.Text.Length, 15))}{(button.Text.Length > 15 ? "..." : "")}\")";
                    break;
                case Entry entry when !string.IsNullOrEmpty(entry.Text):
                    displayName = $"{elementType} (\"{entry.Text.Substring(0, Math.Min(entry.Text.Length, 15))}{(entry.Text.Length > 15 ? "..." : "")}\")";
                    break;
                default:
                    displayName = elementType;
                    break;
            }

            return displayName;
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
                AbsoluteLayout => "🎯",
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