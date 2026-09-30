using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Services.Real;

// Bridges IAdminService onto /api/boardroom (add/remove) and
// /api/reporting/booking-stats. GetUsersAsync uses /api/user, which is
// Administrator-only on the real API - exactly the audience this
// interface is meant for, so no extra guard is needed here; a non-admin
// simply gets a failed call and an empty list back.
public class RealAdminService : IAdminService
{
    private readonly FlexiSpaceApiClient _api;
    private readonly IBookingService _bookings;

    public RealAdminService(FlexiSpaceApiClient api, IBookingService bookings)
    {
        _api = api;
        _bookings = bookings;
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync()
    {
        var users = await _api.GetAsync<List<ApiUser>>("api/user") ?? new();
        return users.Select(u => new User
        {
            Id = u.EntraObjectId,
            Name = $"{u.FirstName} {u.LastName}".Trim(),
            Email = u.Email,
            Role = RealAuthService.MapRole(u.Role),
            LocationId = u.LocationId?.ToString(),
            Password = string.Empty
        }).ToList();
    }

    public async Task<bool> AddRoomAsync(Boardroom room)
    {
        if (!int.TryParse(room.LocationId, out var locationId))
            return false;

        var dto = new
        {
            Name = room.Name,
            Capacity = room.Capacity,
            Status = "Available",
            LocationId = locationId,
            Equipment = Array.Empty<object>()
        };

        return await _api.PostAsync("api/boardroom", dto);
    }

    public async Task<bool> RemoveRoomAsync(string roomId)
    {
        if (!int.TryParse(roomId, out var id)) return false;
        return await _api.DeleteAsync($"api/boardroom/{id}");
    }

    public async Task<ReportSummary> GetReportSummaryAsync()
    {
        var stats = await _api.GetAsync<ApiBookingStats>("api/reporting/booking-stats") ?? new();
        var todays = await _bookings.GetTodaysBookingsAsync();

        int CountFor(string label) =>
            stats.ByStatus.FirstOrDefault(s => string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

        return new ReportSummary
        {
            TotalBookings = stats.TotalBookings,
            TodaysBookings = todays.Count,
            CancelledBookings = CountFor("Cancelled"),
            ConfirmedBookings = CountFor("Confirmed"),
            // The real backend has no Pending/approval status any more
            // (see FlexiSpace.Core.Enums.BookingStatus) - always 0.
            PendingCount = 0,
            MostUsedRoom = stats.ByBoardroom
                .OrderByDescending(b => b.Count)
                .FirstOrDefault()?.Label ?? "—",
            // The reporting endpoint doesn't return an occupancy
            // percentage today (see BookingStatsResponseDto) - 0 here
            // rather than a fabricated number. Worth raising with
            // Khensani/Tino if this stat is wanted on the real dashboard.
            OccupancyPercent = 0
        };
    }
}
