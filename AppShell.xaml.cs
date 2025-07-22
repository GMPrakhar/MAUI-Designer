namespace MAUIDesigner
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
            // Register routes for navigation
            Routing.RegisterRoute("interactive-preview", typeof(InteractivePreviewPage));
        }
    }
}
