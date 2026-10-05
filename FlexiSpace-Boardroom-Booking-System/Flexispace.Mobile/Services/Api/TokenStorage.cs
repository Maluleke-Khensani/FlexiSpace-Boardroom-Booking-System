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

    public Task<string?> GetAccessTokenAsync() => SecureStorage.Default.GetAsync(Key);

    public Task SetAccessTokenAsync(string token) => SecureStorage.Default.SetAsync(Key, token);

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
