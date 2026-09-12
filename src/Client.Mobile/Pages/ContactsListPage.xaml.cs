using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Contacts;

namespace FifthBox.ServerManager.Client.Mobile.Pages;

public partial class ContactsListPage : ContentPage
{
    private readonly IContactsClient _contacts;
    private readonly IAuthClient _auth;

    public ContactsListPage(IContactsClient contacts, IAuthClient auth)
    {
        InitializeComponent();
        _contacts = contacts;
        _auth = auth;
    }

    // Reload each time we return (e.g. after a capture) so a newly saved location shows immediately.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var contacts = await _contacts.ListAsync();
            ContactsView.ItemsSource = contacts.Select(ToRow).ToList();
        }
        catch (ApiException ex)
        {
            await DisplayAlertAsync("Couldn't load contacts", ex.Message, "OK");
        }
        finally
        {
            Refresh.IsRefreshing = false;
        }
    }

    private static ContactRow ToRow(ContactResponse c)
    {
        var hasLocation = c is { Latitude: not null, Longitude: not null };
        var locationText = hasLocation ? $"📍 {c.Latitude:F5}, {c.Longitude:F5}" : string.Empty;
        return new ContactRow(c, c.Category.ToString(), locationText, hasLocation);
    }

    private async void OnRefreshing(object? sender, EventArgs e) => await LoadAsync();

    private async void OnContactSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ContactRow row)
        {
            return;
        }

        ContactsView.SelectedItem = null;
        await Shell.Current.GoToAsync("capture", new Dictionary<string, object> { ["Contact"] = row.Contact });
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}

/// <summary>A contact flattened for the list: the source contact plus display-ready strings.</summary>
public sealed record ContactRow(ContactResponse Contact, string Subtitle, string LocationText, bool HasLocation)
{
    public string Name => Contact.Name;
}
