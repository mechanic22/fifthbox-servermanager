using FifthBox.ServerManager.Client.Core;
using FifthBox.ServerManager.Shared.Auth;

namespace FifthBox.ServerManager.Client.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IAuthClient _auth;

    public LoginPage(IAuthClient auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        SetBusy(true);
        try
        {
            await _auth.LoginAsync(new LoginRequest
            {
                UserName = EmailEntry.Text?.Trim() ?? string.Empty,
                Password = PasswordEntry.Text ?? string.Empty,
            });
            await Shell.Current.GoToAsync("contacts");
        }
        catch (ApiException ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        catch (Exception ex)
        {
            // catch-all so network/storage/nav failures still reach the user
            ErrorLabel.Text = $"Sign-in failed: {ex.Message}";
            ErrorLabel.IsVisible = true;
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
        SignInButton.IsEnabled = !busy;
    }
}
