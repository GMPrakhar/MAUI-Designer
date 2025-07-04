using Microsoft.Maui.Controls;

namespace MAUIDesigner.HelperViews
{
    public class XamlEditorTab : TabMenu
    {
        public Editor XamlEditor { get; }
        public Button GetXamlButton { get; }
        public Button ApplyXamlButton { get; }
        public XamlEditorTab(System.Action getXamlAction, System.Action applyXamlAction)
        {
            TabName = "XAML Editor";
            XamlEditor = new Editor { MinimumHeightRequest = 150, FontAutoScalingEnabled = true, HorizontalOptions = LayoutOptions.FillAndExpand, VerticalOptions = LayoutOptions.FillAndExpand };
            GetXamlButton = new Button { Text = "Get XAML", Margin = new Thickness(0, 0, 0, 10), HorizontalOptions = LayoutOptions.Fill };
            ApplyXamlButton = new Button { Text = "Apply XAML", HorizontalOptions = LayoutOptions.Fill };

            GetXamlButton.Clicked += (s, e) => getXamlAction();
            ApplyXamlButton.Clicked += (s, e) => applyXamlAction();

            var buttonStack = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { GetXamlButton, ApplyXamlButton },
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start
            };

            var mainLayout = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
                },
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand
            };
            mainLayout.Add(buttonStack, 0, 0);
            mainLayout.Add(XamlEditor, 1, 0);

            TabContent = mainLayout;
        }
    }
}