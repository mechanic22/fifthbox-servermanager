using FifthBox.ServerManager.Client.Mobile.Pages;

namespace FifthBox.ServerManager.Client.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("contacts", typeof(ContactsListPage));
        Routing.RegisterRoute("capture", typeof(ContactCapturePage));
    }
}
