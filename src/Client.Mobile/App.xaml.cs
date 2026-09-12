using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Client.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// The seeded MD3 tokens are a light-only palette, so pin the app to light — otherwise the
		// template's default dark-theme control styles (e.g. white Entry text) fight the light surface.
		// Full light/dark theming is a job for the future FifthBox.MaterialComponents.Maui library.
		UserAppTheme = AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}