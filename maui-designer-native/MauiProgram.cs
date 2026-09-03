using CommunityToolkit.Maui;
using MAUIDesigner.LayoutDesigners;
using Microsoft.Extensions.Logging;

namespace MAUIDesigner
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("FluentIcons.ttf", "FluentIcons");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif
            // DevFlow (dotnet/maui-labs) inspects a *running* MAUI app (tree, screenshots,
            // property mutation). It is not a designer. The agent currently targets .NET 10;
            // enable after this app leaves net8: builder.AddMauiDevFlowAgent() in DEBUG.
            // See docs/maui-labs.md.

            builder.Services.AddKeyedSingleton<ILayoutDesigner, GridLayoutDesigner>(typeof(Grid));

            return builder.Build();
        }
    }
}
