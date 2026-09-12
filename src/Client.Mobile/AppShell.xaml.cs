using FifthBox.ServerManager.Client.Mobile.Pages;

namespace FifthBox.ServerManager.Client.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pushed (non-root) routes. Login switches to "contacts"; a tapped contact pushes "capture".
        Routing.RegisterRoute("contacts", typeof(ContactsListPage));
        Routing.RegisterRoute("capture", typeof(ContactCapturePage));
    }
}
