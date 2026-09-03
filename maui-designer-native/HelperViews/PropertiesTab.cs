using Microsoft.Maui.Controls;

namespace MAUIDesigner.HelperViews
{
    public class PropertiesTab : TabMenu
    {
        public PropertiesTab()
        {
            TabName = "Properties";
            var layout = new ScrollView
            {
                Content = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(10,0,10,0) }
            };
            TabContent = layout;
        }

        public VerticalStackLayout PropertiesLayout => ((TabContent as ScrollView)?.Content as VerticalStackLayout)!;
    }
}