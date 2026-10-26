using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Api;

namespace Flexispace.Web.Services;

public interface ICatalogManagementService
{
    Task<IReadOnlyList<EquipmentDto>> GetEquipmentAsync();
    Task<IReadOnlyList<CateringDto>> GetCateringAsync();
    Task<(bool Ok, string Message)> CreateEquipmentAsync(string name, string description);
    Task<(bool Ok, string Message)> UpdateEquipmentAsync(EquipmentDto item);
    Task<(bool Ok, string Message)> DeleteEquipmentAsync(int id);
    Task<(bool Ok, string Message)> CreateCateringAsync(string name, string? description);
    Task<(bool Ok, string Message)> UpdateCateringAsync(CateringDto item);
    Task<(bool Ok, string Message)> DeleteCateringAsync(int id);
}

public sealed class ApiCatalogManagementService(FlexiSpaceApiClient api, IAuthService auth, IRoomService rooms) : ICatalogManagementService
{
    private bool IsAdministrator => auth.CurrentUser?.Role == UserRole.Administrator;

    public async Task<IReadOnlyList<EquipmentDto>> GetEquipmentAsync() =>
        IsAdministrator ? await api.GetAsync<List<EquipmentDto>>("api/equipment") ?? [] : [];

    public async Task<IReadOnlyList<CateringDto>> GetCateringAsync() =>
        IsAdministrator ? await api.GetAsync<List<CateringDto>>("api/catering") ?? [] : [];

    public Task<(bool Ok, string Message)> CreateEquipmentAsync(string name, string description) =>
        SendAsync(() => api.PostAsJsonAsync("api/equipment", new { Name = name.Trim(), Description = description.Trim() }));

    public Task<(bool Ok, string Message)> UpdateEquipmentAsync(EquipmentDto item) =>
        SendAsync(() => api.PutAsJsonAsync($"api/equipment/{item.Id}", new { Name = item.Name.Trim(), Description = item.Description.Trim(), item.IsActive }));

    public Task<(bool Ok, string Message)> DeleteEquipmentAsync(int id) =>
        SendAsync(() => api.DeleteAsync($"api/equipment/{id}"));

    public Task<(bool Ok, string Message)> CreateCateringAsync(string name, string? description) =>
        SendAsync(() => api.PostAsJsonAsync("api/catering", new { Name = name.Trim(), Description = description?.Trim() }));

    public Task<(bool Ok, string Message)> UpdateCateringAsync(CateringDto item) =>
        SendAsync(() => api.PutAsJsonAsync($"api/catering/{item.Id}", new { Name = item.Name.Trim(), Description = item.Description?.Trim(), item.IsActive }));

    public Task<(bool Ok, string Message)> DeleteCateringAsync(int id) =>
        SendAsync(() => api.DeleteAsync($"api/catering/{id}"));

    private async Task<(bool Ok, string Message)> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        if (!IsAdministrator)
            return (false, "Only Administrators can manage equipment and catering options.");

        using var response = await send();
        if (response.IsSuccessStatusCode)
        {
            rooms.InvalidateCatalog();
            return (true, "Saved successfully.");
        }

        return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));
    }
}
