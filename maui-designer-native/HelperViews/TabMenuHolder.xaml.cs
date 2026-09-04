using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace MAUIDesigner.HelperViews
{
    public partial class TabMenuHolder : ContentView
    {
        private readonly List<TabMenu> _tabMenus = new();
        private TabMenu? _selectedTab;
        public bool IsSidebar { get; set; } = false;

        public TabMenuHolder()
        {
            InitializeComponent();
        }

        public void AddTab(TabMenu tabMenu)
        {
            _tabMenus.Add(tabMenu);
            // Remove "Tab" from the tab name if present
            var displayName = tabMenu.TabName?.Replace("Tab", string.Empty).Trim();
            var tabLabel = new Label
            {
                Text = displayName,
                FontSize = 12,
                Padding = new Thickness(5, 2),
                BackgroundColor = Color.FromArgb("#33343a"),
                TextColor = Colors.White,
                Margin = new Thickness(2, 0),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold
            };
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) => SelectTab(tabMenu);
            tabLabel.GestureRecognizers.Add(tapGesture);
            TabHeaderPanelBottom.Children.Add(tabLabel);
            if (_tabMenus.Count == 1)
                SelectTab(tabMenu);
        }

        private void SelectTab(TabMenu tabMenu)
        {
            _selectedTab = tabMenu;
            // Show the tab's header (if any) and content
            var displayName = tabMenu.TabName?.Replace("Tab", string.Empty).Trim();
            if (!string.IsNullOrEmpty(displayName))
                TabHeaderContent.Content = new Label { Text = displayName, FontSize = 16, Padding = new Thickness(8, 4), TextColor = Colors.LightGray };
            else
                TabHeaderContent.Content = null;
            TabContentHolder.Content = tabMenu;
            // Update label styles
            for (int i = 0; i < TabHeaderPanelBottom.Children.Count; i++)
            {
                if (TabHeaderPanelBottom.Children[i] is Label lbl)
                    lbl.BackgroundColor = _tabMenus[i] == tabMenu ? Color.FromArgb("#44464d") : Color.FromArgb("#33343a");
            }
        }
    }
}
