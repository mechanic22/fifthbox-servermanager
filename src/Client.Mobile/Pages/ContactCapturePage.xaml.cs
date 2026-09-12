using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Contacts;

namespace FifthBox.ServerManager.Client.Mobile.Pages;

// The contact is passed as a navigation parameter from the list; MAUI sets it via the query property.
[QueryProperty(nameof(Contact), "Contact")]
public partial class ContactCapturePage : ContentPage
{
    private readonly ILocationProvider _location;
    private readonly IContactsClient _contacts;
    private DeviceLocation? _captured;
    private ContactResponse? _contact;

    public ContactCapturePage(ILocationProvider location, IContactsClient contacts)
    {
        InitializeComponent();
        _location = location;
        _contacts = contacts;
    }

    public ContactResponse? Contact
    {
        get => _contact;
        set
        {
            _contact = value;
            ShowContact();
        }
    }

    private void ShowContact()
    {
        if (_contact is null)
        {
            return;
        }

        NameLabel.Text = _contact.Name;
        if (_contact is { Latitude: not null, Longitude: not null })
        {
            LocationLabel.Text = $"{_contact.Latitude:F5}, {_contact.Longitude:F5}";
        }
    }

    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            _captured = await _location.GetCurrentAsync();
            if (_captured is null)
            {
                await DisplayAlertAsync("Location unavailable", "Permission was denied or no fix is available.", "OK");
                return;
            }

            LocationLabel.Text = $"{_captured.Latitude:F5}, {_captured.Longitude:F5}";
            SaveButton.IsEnabled = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_contact is null || _captured is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            // Send the full contact so the update preserves its other fields; the server merges in the
            // new coordinates. Once saved, every client (including the web app) can read them.
            await _contacts.UpdateAsync(_contact.Id, new UpdateContactRequest
            {
                Name = _contact.Name,
                Email = _contact.Email,
                Phone = _contact.Phone,
                Category = _contact.Category,
                Tags = _contact.Tags,
                Latitude = _captured.Latitude,
                Longitude = _captured.Longitude,
            });
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            await DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
    }
}
