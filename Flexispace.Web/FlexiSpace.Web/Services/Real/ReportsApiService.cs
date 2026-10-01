namespace Flexispace.Web.Services.Real;

// Talks to the API's reporting and audit endpoints for the /reports page:
//   GET api/reporting/booking-stats       - Centre Manager (own location) / Administrator
//   GET api/reporting/bookings/export     - same, as a CSV file
//   GET api/auditlog/recent               - Administrator
//   GET api/auditlog/entity/{name}/{id}   - Administrator
// The API enforces who can see what; a caller without the role simply
// gets nothing back (null / empty list).
public class ReportsApiService
{
    private readonly FlexiSpaceApiClient _api;

    public ReportsApiService(FlexiSpaceApiClient api)
    {
        _api = api;
    }

    public Task<ApiBookingStats?> GetBookingStatsAsync(DateOnly? from, DateOnly? to, string? locationId) =>
        _api.GetAsync<ApiBookingStats>("api/reporting/booking-stats" + BuildQuery(from, to, locationId));

    public async Task<(byte[]? Content, string FileName)> ExportBookingsCsvAsync(
        DateOnly? from, DateOnly? to, string? locationId)
    {
        var (content, fileName) = await _api.GetFileAsync(
            "api/reporting/bookings/export" + BuildQuery(from, to, locationId));

        return (content, string.IsNullOrWhiteSpace(fileName)
            ? $"flexispace-bookings-{DateTime.Now:yyyyMMdd-HHmm}.csv"
            : fileName);
    }

    public async Task<List<ApiAuditLog>> GetRecentAuditAsync(int count) =>
        await _api.GetAsync<List<ApiAuditLog>>($"api/auditlog/recent?count={count}") ?? new();

    public async Task<List<ApiAuditLog>> GetRecordHistoryAsync(string entityName, string entityId) =>
        await _api.GetAsync<List<ApiAuditLog>>(
            $"api/auditlog/entity/{Uri.EscapeDataString(entityName)}/{Uri.EscapeDataString(entityId)}") ?? new();

    private static string BuildQuery(DateOnly? from, DateOnly? to, string? locationId)
    {
        var parts = new List<string>();
        if (from.HasValue) parts.Add($"fromDate={from.Value:yyyy-MM-dd}");
        if (to.HasValue) parts.Add($"toDate={to.Value:yyyy-MM-dd}");
        if (int.TryParse(locationId, out var id)) parts.Add($"locationId={id}");
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }
}
