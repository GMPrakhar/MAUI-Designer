using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace MAUIDesigner
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Set dark theme as default
            UserAppTheme = AppTheme.Dark;
            MainPage = new AppShell();
        }
    }
}
