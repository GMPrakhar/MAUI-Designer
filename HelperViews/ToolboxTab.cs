using Microsoft.Maui.Controls;

namespace MAUIDesigner.HelperViews
{
    public class ToolboxTab : TabMenu
    {
        public ToolboxTab()
        {
            TabName = "Toolbox";
            var layout = new ScrollView
            {
                Content = new VerticalStackLayout { Margin = new Thickness(10), Spacing = 4}
            };
            TabContent = layout;
            ToolboxLayout = (layout.Content as VerticalStackLayout)!;
        }

        public VerticalStackLayout ToolboxLayout { get; }
    }
}