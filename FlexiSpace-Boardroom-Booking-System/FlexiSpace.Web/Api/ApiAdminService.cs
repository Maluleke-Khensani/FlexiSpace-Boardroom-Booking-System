using System.Net.Http.Json;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

public sealed class ApiAdminService(
    FlexiSpaceApiClient api,
    IAuthService auth,
    IRoomService rooms,
    IBookingService bookings) : IAdminService
{
    public async Task<IReadOnlyList<User>> GetUsersAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return [];

        var dtos = await api.GetAsync<List<UserDto>>("api/user") ?? [];
        return dtos.Select(CatalogMapper.ToUser).ToList();
    }

    public async Task<(bool Ok, string Message)> CreateUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can add users.");

        int? locationId = null;
        if (!string.IsNullOrWhiteSpace(user.LocationId))
        {
            if (!IdMap.TryToInt(user.LocationId, out var parsed))
                return (false, "Choose a valid location.");
            locationId = parsed;
        }

        var response = await api.PostAsJsonAsync("api/user", new UserCreatePayload
        {
            FirstName = user.FirstName.Trim(),
            LastName = user.LastName.Trim(),
            Email = user.Email.Trim(),
            Role = CatalogMapper.ToApiRole(user.Role),
            LocationId = locationId
        });

        if (!response.IsSuccessStatusCode)
            return (false, await FlexiSpaceApiClient.ReadErrorAsync(response));

        var created = await response.Content.ReadFromJsonAsync<UserCreateResultDto>(FlexiSpaceApiClient.Json);
        return (true, string.IsNullOrWhiteSpace(created?.Message) ? "User added." : created.Message);
    }

    public async Task<(bool Ok, string Message)> UpdateUserAsync(User user)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can update users.");
        if (user.ApiId <= 0)
            return (false, "User not found.");

        int? locationId = null;
        if (!string.IsNullOrWhiteSpace(user.LocationId))
        {
            if (!IdMap.TryToInt(user.LocationId, out var parsed))
                return (false, "Choose a valid location.");
            locationId = parsed;
        }

        var response = await api.PutAsJsonAsync($"api/user/{user.ApiId}", new UserUpdatePayload
        {
            FirstName = user.FirstName.Trim(),
            LastName = user.LastName.Trim(),
            Email = user.Email.Trim(),
            Role = CatalogMapper.ToApiRole(user.Role),
            LocationId = locationId
        });

        return response.IsSuccessStatusCode
            ? (true, "User updated.")
            : (false, await FlexiSpaceApiClient.ReadErrorAsync(response));
    }

    public async Task<(bool Ok, string Message)> RemoveUserAsync(int apiId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return (false, "Only Administrators can remove users.");
        if (apiId <= 0)
            return (false, "This user could not be identified. Refresh the page and try again.");

        var response = await api.DeleteAsync($"api/user/{apiId}");
        return response.IsSuccessStatusCode
            ? (true, "User removed.")
            : (false, await FlexiSpaceApiClient.ReadErrorAsync(response));
    }

    public async Task<bool> AddRoomAsync(Boardroom room)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return false;
        if (string.IsNullOrWhiteSpace(room.Name) || !IdMap.TryToInt(room.LocationId, out var locationId))
            return false;

        var payload = new BoardroomCreatePayload
        {
            Name = room.Name,
            Capacity = Math.Max(1, room.Capacity),
            LocationId = locationId,
            Status = 0,
            Equipment = []
        };

        var response = await api.PostAsJsonAsync("api/boardroom", payload);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveRoomAsync(string roomId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return false;
        if (!IdMap.TryToInt(roomId, out var id))
            return false;

        var response = await api.DeleteAsync($"api/boardroom/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<ReportSummary> GetReportSummaryAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanViewReports(auth.CurrentUser.Role))
            return new ReportSummary();

        var all = await bookings.GetBookingsAsync();
        var active = all.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var mostUsed = active
            .GroupBy(b => b.RoomName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "—";

        var roomCount = Math.Max(1, (await rooms.GetRoomsAsync()).Count);
        var slotsToday = roomCount * 8.0;
        var occupiedHours = active
            .Where(b => b.Start.Date == DateTime.Today)
            .Sum(b => (b.End - b.Start).TotalHours);

        return new ReportSummary
        {
            TotalBookings = all.Count,
            TodaysBookings = all.Count(b => b.Start.Date == DateTime.Today && b.Status != BookingStatus.Cancelled),
            CancelledBookings = all.Count(b => b.Status == BookingStatus.Cancelled),
            ConfirmedBookings = all.Count(b => b.Status == BookingStatus.Confirmed),
            PendingCount = all.Count(b => b.Status == BookingStatus.Pending),
            MostUsedRoom = mostUsed,
            OccupancyPercent = Math.Round(Math.Min(100, occupiedHours / slotsToday * 100), 1)
        };
    }
}
