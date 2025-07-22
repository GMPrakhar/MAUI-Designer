namespace MAUIDesigner.Resources
{
    public static class DefaultXaml
    {
        public const string Content = """
            <?xml version="1.0" encoding="utf-8" ?>
            <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
            >
            <AbsoluteLayout
                Margin="20,20,20,20"
                IsPlatformEnabled="True"
                StyleId="designerFrame"
            >

            <Button
                Text="Login"
                Margin="114,150,205,195"
                HeightRequest="45"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                WidthRequest="91"
                IsPlatformEnabled="True"
            />

            <BoxView
                Margin="5,-16,329,249"
                BackgroundColor="#17FFFFFF"
                HeightRequest="265"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                WidthRequest="324"
                IsPlatformEnabled="True"
            />

            <Label
                Text="Username "
                Margin="26,21,25,20"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                IsPlatformEnabled="True"
            />

            <Label
                Text="Password "
                Margin="26,70,25,69"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                IsPlatformEnabled="True"
            />

            <Line
                Margin="14,378,13,377"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                IsPlatformEnabled="True"
            />

            <Editor
                Text="Type here"
                TextColor="#FF404040"
                Margin="110,16,305,48"
                HeightRequest="32"
                IsEnabled="True"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                WidthRequest="195"
                IsPlatformEnabled="True"
            />

            <Editor
                Text="Type here"
                TextColor="#FF404040"
                Margin="111,67,311,97"
                HeightRequest="30"
                IsEnabled="True"
                MinimumHeightRequest="20"
                MinimumWidthRequest="20"
                WidthRequest="200"
                IsPlatformEnabled="True"
            />

            </AbsoluteLayout>

            </ContentPage>
            """;
    }
}