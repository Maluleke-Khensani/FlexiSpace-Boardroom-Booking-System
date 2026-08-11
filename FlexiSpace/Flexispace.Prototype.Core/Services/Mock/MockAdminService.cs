using Flexispace.Core.Helpers;
using Flexispace.Core.Models;

namespace Flexispace.Core.Services.Mock;

public class MockAdminService(MockDataStore store, IAuthService auth, IRoomService rooms) : IAdminService
{
    public Task<IReadOnlyList<User>> GetUsersAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageUsers(auth.CurrentUser.Role))
            return Task.FromResult<IReadOnlyList<User>>([]);

        return Task.FromResult<IReadOnlyList<User>>(SeedData.Users.ToList());
    }

    public async Task<bool> AddRoomAsync(Boardroom room)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return false;

        if (string.IsNullOrWhiteSpace(room.Name) || string.IsNullOrWhiteSpace(room.LocationId))
            return false;

        room.Id = $"{room.LocationId}-{Guid.NewGuid().ToString("N")[..6]}";
        SeedData.Rooms.Add(room);
        await Task.CompletedTask;
        return true;
    }

    public Task<bool> RemoveRoomAsync(string roomId)
    {
        if (auth.CurrentUser is null || !RolePermissions.CanManageRooms(auth.CurrentUser.Role))
            return Task.FromResult(false);

        var room = SeedData.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return Task.FromResult(false);
        SeedData.Rooms.Remove(room);
        return Task.FromResult(true);
    }

    public async Task<ReportSummary> GetReportSummaryAsync()
    {
        if (auth.CurrentUser is null || !RolePermissions.CanViewReports(auth.CurrentUser.Role))
            return new ReportSummary();

        var bookings = store.Bookings;
        var active = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var mostUsed = active
            .GroupBy(b => b.RoomName)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? "—";

        var roomCount = Math.Max(1, (await rooms.GetRoomsAsync()).Count);
        var slotsToday = roomCount * 8.0; // rough 8 hour day
        var occupiedHours = active
            .Where(b => b.Start.Date == DateTime.Today)
            .Sum(b => (b.End - b.Start).TotalHours);

        return new ReportSummary
        {
            TotalBookings = bookings.Count,
            TodaysBookings = bookings.Count(b => b.Start.Date == DateTime.Today && b.Status != BookingStatus.Cancelled),
            CancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            ConfirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed),
            PendingCount = bookings.Count(b => b.Status == BookingStatus.Pending),
            MostUsedRoom = mostUsed,
            OccupancyPercent = Math.Round(Math.Min(100, occupiedHours / slotsToday * 100), 1)
        };
    }
}

