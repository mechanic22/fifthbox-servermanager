using Microsoft.UI.Xaml;

namespace FifthBox.ServerManager.Client.Mobile.WinUI;

public partial class App : MauiWinUIApplication
{
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

