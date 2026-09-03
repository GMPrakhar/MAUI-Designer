using Microsoft.Maui.Controls;

namespace MAUIDesigner.HelperViews
{
    public class XamlEditorTab : TabMenu
    {
        public Editor XamlEditor { get; }
        public Button GetXamlButton { get; }
        public Button ApplyXamlButton { get; }
        public Label StatusLabel { get; }
        
        public XamlEditorTab(System.Action getXamlAction, System.Action applyXamlAction)
        {
            TabName = "XAML Editor";
            XamlEditor = new Editor { 
                MinimumHeightRequest = 150, 
                FontAutoScalingEnabled = true, 
                FontFamily = "Consolas,Monaco,monospace",
                FontSize = 12,
                BackgroundColor = Color.FromArgb("#0F1117"),
                TextColor = Color.FromArgb("#E6E8EE"),
                HorizontalOptions = LayoutOptions.Fill, 
                VerticalOptions = LayoutOptions.Fill 
            };
            GetXamlButton = new Button { 
                Text = "Get XAML", 
                Margin = new Thickness(8, 8, 8, 4), 
                HorizontalOptions = LayoutOptions.Fill 
            };
            ApplyXamlButton = new Button { 
                Text = "Apply XAML", 
                Margin = new Thickness(8, 0, 8, 4), 
                HorizontalOptions = LayoutOptions.Fill 
            };
            StatusLabel = new Label { 
                Text = "Ready", 
                FontSize = 10, 
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Fill 
            };

            GetXamlButton.Clicked += (s, e) => 
            {
                try
                {
                    getXamlAction();
                    UpdateStatus("XAML generated successfully", Colors.Green);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error generating XAML: {ex.Message}", Colors.Red);
                }
            };
            
            ApplyXamlButton.Clicked += (s, e) => 
            {
                try
                {
                    applyXamlAction();
                    UpdateStatus("XAML applied successfully", Colors.Green);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error applying XAML: {ex.Message}", Colors.Red);
                }
            };

            var buttonStack = new VerticalStackLayout
            {
                Spacing = 5,
                Children = { GetXamlButton, ApplyXamlButton, StatusLabel },
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
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            mainLayout.Add(buttonStack, 0, 0);
            mainLayout.Add(XamlEditor, 1, 0);

            TabContent = mainLayout;
        }

        private void UpdateStatus(string message, Color color)
        {
            StatusLabel.Text = message;
            StatusLabel.TextColor = color;
            
            // Clear status after 3 seconds
            Application.Current?.Dispatcher.StartTimer(TimeSpan.FromSeconds(3), () =>
            {
                StatusLabel.Text = "Ready";
                StatusLabel.TextColor = Colors.Gray;
                return false;
            });
        }
    }
}