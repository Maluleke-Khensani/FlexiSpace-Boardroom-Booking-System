using Flexispace.Core.Models;

namespace Flexispace.Core.Services;

public interface IAdminService
{
    Task<IReadOnlyList<User>> GetUsersAsync();
    Task<bool> AddRoomAsync(Boardroom room);
    Task<bool> RemoveRoomAsync(string roomId);
    Task<ReportSummary> GetReportSummaryAsync();
}

public class ReportSummary
{
    public int TotalBookings { get; set; }
    public int TodaysBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int PendingCount { get; set; }
    public string MostUsedRoom { get; set; } = "—";
    public double OccupancyPercent { get; set; }
}

