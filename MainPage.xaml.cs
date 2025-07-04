using MAUIDesigner.DnDHelper;
using MAUIDesigner.HelperViews;
using static MAUIDesigner.DnDHelper.ScalingHelper;

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
    }

}
