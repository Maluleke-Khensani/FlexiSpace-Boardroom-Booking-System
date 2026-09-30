using Flexispace.Mobile.Models;

namespace Flexispace.Mobile.Services.Real;

// Bridges IAdminService onto /api/boardroom (add/remove), /api/user, and
// /api/reporting/booking-stats. GetUsersAsync's endpoint is
// Administrator-only on the real API - exactly the audience this
// interface is meant for, so no extra guard is needed here; anyone else
// simply gets a failed call and an empty list back.
//
// GetReportSummaryAsync fills in only what the real backend actually
// exposes today. ReportSummary has several fields (UsersByRole,
// WeekdayActivity, RecentActivity, BlockedPeriods, AlertsGenerated,
// OccupancyPercent, and the various per-role user counts) that have no
// backing endpoint yet - left at their class defaults (0 / "—" / empty
// list) rather than fabricated. Worth raising with Khensani/Tino if the
// real dashboard needs any of these - most would need small additions to
// ReportingController rather than anything client-side.
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
        var users = await GetUsersAsync();
        var todays = await _bookings.GetTodaysBookingsAsync();

        int CountFor(string label) =>
            stats.ByStatus.FirstOrDefault(s => string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

        return new ReportSummary
        {
            HasAccess = true,
            ScopeLabel = "All locations",
            GeneratedAt = DateTime.Now,

            TotalUsers = users.Count,
            StaffCount = users.Count(u => u.Role == UserRole.Staff),
            CentreManagerCount = users.Count(u => u.Role == UserRole.CentreManager),
            AdministratorCount = users.Count(u => u.Role == UserRole.Administrator),
            ClientUsers = users.Count(u => u.Role == UserRole.Client),
            EmployeeUsers = users.Count(u => u.Role != UserRole.Client),

            TotalBookings = stats.TotalBookings,
            TodaysBookings = todays.Count,
            CancelledBookings = CountFor("Cancelled"),
            ConfirmedBookings = CountFor("Confirmed"),
            AverageAttendees = stats.AverageAttendees,

            RoomsInScope = (await _bookings.GetBookingsAsync()).Select(b => b.RoomId).Distinct().Count(),
            MostUsedRoom = stats.ByBoardroom
                .OrderByDescending(b => b.Count)
                .FirstOrDefault()?.Label ?? "—",
            MostUsedLocation = stats.ByLocation
                .OrderByDescending(l => l.Count)
                .FirstOrDefault()?.Label ?? "—",

            BookingsByLocation = stats.ByLocation
                .Select(l => new NamedCount { Name = l.Label, Count = l.Count })
                .ToList(),
            BookingsByStatus = stats.ByStatus
                .Select(s => new NamedCount { Name = s.Label, Count = s.Count })
                .ToList(),
            TopRooms = stats.ByBoardroom
                .OrderByDescending(b => b.Count)
                .Select(b => new NamedCount { Name = b.Label, Count = b.Count })
                .ToList()

            // OccupancyPercent, ThisWeekBookings/ThisMonthBookings/
            // UpcomingBookings, ActiveBookers/BookingsPerActiveUser/
            // AvgBookingsPerDayThisWeek, AlertsGenerated/UnreadAlerts,
            // BlockedPeriods, UsersByRole, WeekdayActivity, RecentActivity
            // and BusiestDayLabel all stay at their class defaults - the
            // real API doesn't return the underlying data for these yet.
        };
    }
}
