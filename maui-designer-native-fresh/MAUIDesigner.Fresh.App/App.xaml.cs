using Microsoft.Extensions.DependencyInjection;

namespace MAUIDesigner.Fresh.App;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_services.GetRequiredService<MainPage>())
		{
			Title = "MAUI Designer"
		};
	}
}