using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Client.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// seeded MD3 tokens are light-only, so pin light or the dark control styles fight it
		// NOTE: real light/dark waits for FifthBox.MaterialComponents.Maui
		UserAppTheme = AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}