using Microsoft.Maui.Controls;

namespace MAUIDesigner.HelperViews
{
    public partial class TabMenu : ContentView
    {
        public static readonly BindableProperty TabNameProperty = BindableProperty.Create(
            nameof(TabName), typeof(string), typeof(TabMenu), default(string));

        public static readonly BindableProperty TabContentProperty = BindableProperty.Create(
            nameof(TabContent), typeof(View), typeof(TabMenu), default(View), propertyChanged: OnTabContentChanged);

        public string TabName
        {
            get => (string)GetValue(TabNameProperty);
            set => SetValue(TabNameProperty, value);
        }

        public View TabContent
        {
            get => (View)GetValue(TabContentProperty);
            set => SetValue(TabContentProperty, value);
        }

        public bool IsSidebar { get; set; } = false;

        public TabMenu()
        {
            InitializeComponent();
        }

        private static void OnTabContentChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TabMenu tabMenu && newValue is View view)
            {
                    tabMenu.TabContentHolder.Content = view;
            }
        }
    }
}
