namespace Flexispace.Mobile.Services.Api;

public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task SetAccessTokenAsync(string token);
    Task ClearAsync();
}

public sealed class SecureTokenStorage : ITokenStorage
{
    private const string Key = "flexispace.access_token";

    /// <summary>
    /// While a guest is browsing, ignore any token left from an earlier sign-in.
    /// The token itself stays in secure storage; this flag is memory only.
    /// </summary>
    public static bool SuppressStoredToken { get; set; }

    public Task<string?> GetAccessTokenAsync() =>
        SuppressStoredToken ? Task.FromResult<string?>(null) : SecureStorage.Default.GetAsync(Key);

    public Task SetAccessTokenAsync(string token) => SecureStorage.Default.SetAsync(Key, token);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
