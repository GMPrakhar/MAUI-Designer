using Microsoft.Maui.Platform;
using System.Runtime.CompilerServices;
using Extensions = Microsoft.Maui.Controls.Xaml.Extensions;

namespace MAUIDesigner
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public IEnumerable<string> ElementItems { get; set; }

        public MainPage()
        {
            InitializeComponent();
        }

        private void RenderXAML(object sender, EventArgs e)
        {
            var xaml = Editor.Text;
            try
            {
                Extensions.LoadFromXaml(Shower, xaml);
            }
            catch (Exception)
            {
                Application.Current.MainPage.DisplayAlert("Error", "Invalid XAML", "OK");
            }
        }
    }

}
