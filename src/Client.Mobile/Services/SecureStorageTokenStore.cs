using FifthBox.ServerManager.Client.Core;
using Microsoft.Maui.Storage;

namespace FifthBox.ServerManager.Client.Mobile.Services;

/// NOTE: plaintext Preferences fallback when the keychain's unavailable (unsigned MacCatalyst dev)
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string AccessKey = "fb_access_token";
    private const string RefreshKey = "fb_refresh_token";
    private const string ExpiresKey = "fb_expires_at";

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) => GetAsync(AccessKey);
    public Task<string?> GetRefreshTokenAsync(CancellationToken ct = default) => GetAsync(RefreshKey);

    public async Task SaveAsync(TokenPair tokens, CancellationToken ct = default)
    {
        await SetAsync(AccessKey, tokens.AccessToken);
        await SetAsync(RefreshKey, tokens.RefreshToken);
        await SetAsync(ExpiresKey, tokens.ExpiresAt.ToString("O"));
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        try { SecureStorage.Default.RemoveAll(); } catch { /* keychain unavailable — prefs below */ }
        Preferences.Default.Remove(AccessKey);
        Preferences.Default.Remove(RefreshKey);
        Preferences.Default.Remove(ExpiresKey);
        return Task.CompletedTask;
    }

    private static async Task<string?> GetAsync(string key)
    {
        try
        {
            var value = await SecureStorage.Default.GetAsync(key);
            if (value is not null)
            {
                return value;
            }
        }
        catch
        {
            // fall back to Preferences
        }

        return Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string?>(key, null) : null;
    }

    private static async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            Preferences.Default.Set(key, value);
        }
    }
}
