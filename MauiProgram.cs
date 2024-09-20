
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

            builder.Services.AddKeyedSingleton<ILayoutDesigner, GridLayoutDesigner>(typeof(Grid));
            //builder.Services.AddKeyedSingleton<ILayoutDesigner, AbsoluteLayoutDesigner>(typeof(AbsoluteLayout));


            return builder.Build();
        }
    }
}
