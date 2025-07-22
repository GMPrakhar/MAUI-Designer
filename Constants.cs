namespace MAUIDesigner
{
    public static class Constants
    {
        public static string[] FrameworkElementNames = { "EncapsulatingView", "topLeftRect", "topRightRect", "bottomLeftRect", "bottomRightRect", "ElementBorder" };
        
        // Toolbox constants
        public const int ToolBoxItemLabelSize = 12;
        public const int ToolBoxItemLabelAnimateSize = 15;
        public const int ToolBoxItemImageHeight = 15;
        public const int ToolBoxItemImageWidth = 15;
        
        // Panel sizing constants  
        public const double MinimumPanelWidth = 50;
        public const double MinimumPanelHeight = 50;
        
        // Animation constants
        public const uint AnimationDuration = 100;
        public const uint AnimationRate = 16;
        
        // XAML constants
        public const string XmlHeader = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>";
        public const string ContentPageOpenTag = "<ContentPage xmlns=\"http://schemas.microsoft.com/dotnet/2021/maui\"\r\n             xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\"\r\n>";
        public const string ContentPageCloseTag = "</ContentPage>";
    }
}
