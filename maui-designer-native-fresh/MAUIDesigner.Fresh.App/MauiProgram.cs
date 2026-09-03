using CommunityToolkit.Maui;
using MAUIDesigner.Fresh.App.Catalog;
using MAUIDesigner.Fresh.App.PropertyEditing;
using MAUIDesigner.Fresh.App.Rendering;
using MAUIDesigner.Fresh.App.Workspace;
using MAUIDesigner.Fresh.App.Xaml;
using MAUIDesigner.Fresh.Core.Xaml;
using Microsoft.Extensions.Logging;
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace MAUIDesigner.Fresh.App;

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
			});

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent(options =>
		{
			options.Port = 9223;
			options.EnableLayoutDiagnostics = true;
			options.EnableProfiler = true;
		});
#endif

		builder.Services.AddSingleton<IControlCatalog>(services =>
		{
			var catalog = new ReflectionControlCatalog(services);
			catalog.RegisterAssembly(typeof(View).Assembly);
			catalog.RegisterAssembly(typeof(CommunityToolkit.Maui.Views.DrawingView).Assembly);
			catalog.RegisterAssembly(typeof(App).Assembly);
			return catalog;
		});
		builder.Services.AddSingleton<AssemblyExtensionLoader>();
		builder.Services.AddSingleton<MAUIDesigner.Fresh.Core.Xaml.IXamlTypeResolver, CatalogXamlTypeResolver>();
		builder.Services.AddSingleton<XamlWorkspace>();
		builder.Services.AddSingleton<DesignerWorkspace>();
		builder.Services.AddSingleton<ControlMaterializer>();
		builder.Services.AddSingleton<PropertyEditorRegistry>();
		builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
