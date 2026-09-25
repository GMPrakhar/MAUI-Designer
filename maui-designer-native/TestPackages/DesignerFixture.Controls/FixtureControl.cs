using DesignerFixture.Dependency;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Hosting;

namespace DesignerFixture.Controls;

public sealed class FixtureControl : View
{
    public FixtureControl()
    {
        FixtureMarker.Constructed = true;
    }
}

public static class FixtureHostingExtensions
{
    public static MauiAppBuilder ConfigureFixture(this MauiAppBuilder builder)
    {
        FixtureMarker.Configured = true;
        return builder;
    }
}
