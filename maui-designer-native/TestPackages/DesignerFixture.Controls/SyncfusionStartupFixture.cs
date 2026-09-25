using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.Hosting;

namespace Syncfusion.Maui.Core.Hosting;

public static class AppHostBuilderExtensions
{
    private sealed class MauiControlsInitializer : IMauiInitializeScopedService
    {
        public void Initialize(IServiceProvider services)
        {
        }
    }

    public static MauiAppBuilder ConfigureSyncfusionCore(MauiAppBuilder builder)
    {
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IMauiInitializeScopedService, MauiControlsInitializer>());
        return builder;
    }
}
