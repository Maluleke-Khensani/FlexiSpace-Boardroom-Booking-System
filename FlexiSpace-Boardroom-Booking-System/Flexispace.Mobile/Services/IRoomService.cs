using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services;

public interface IRoomService
{
    Task<IReadOnlyList<OfficeLocation>> GetLocationsAsync();
    Task<OfficeLocation?> GetLocationAsync(string locationId);
    Task<IReadOnlyList<Boardroom>> GetRoomsAsync(string? locationId = null);
    Task<Boardroom?> GetRoomAsync(string roomId);
    Task<IReadOnlyList<Boardroom>> GetAvailabilityAsync(string? locationId, DateTime date);
}
