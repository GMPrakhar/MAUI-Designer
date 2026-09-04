namespace MAUIDesigner.Resources
{
    public static class DefaultXaml
    {
        public const string Content = """
            <?xml version="1.0" encoding="utf-8" ?>
            <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
            <AbsoluteLayout Margin="24">
                <Label Text="Sign in"
                       FontSize="24"
                       FontAttributes="Bold"
                       Margin="24,24,0,0"
                       HeightRequest="36"
                       WidthRequest="200"/>
                <Label Text="Username"
                       Margin="24,72,0,0"
                       HeightRequest="24"
                       WidthRequest="120"/>
                <Entry Placeholder="you@example.com"
                       Margin="24,100,0,0"
                       HeightRequest="40"
                       WidthRequest="280"/>
                <Label Text="Password"
                       Margin="24,156,0,0"
                       HeightRequest="24"
                       WidthRequest="120"/>
                <Entry Placeholder="password"
                       Margin="24,184,0,0"
                       HeightRequest="40"
                       WidthRequest="280"/>
                <Button Text="Continue"
                        Margin="24,244,0,0"
                        HeightRequest="44"
                        WidthRequest="160"/>
            </AbsoluteLayout>
            </ContentPage>
            """;
    }
}
