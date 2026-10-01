using System.Text;
using System.Text.Json;
using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;
using Flexispace.Web.Services.Real;

namespace Flexispace.Web.ViewModels;

// Reports & audit page (/reports).
// - Bookings report: totals and breakdowns from GET api/reporting/booking-stats,
//   plus a CSV download of the same filtered bookings. Centre Managers see
//   their own location only (the API enforces this); Administrators can
//   pick any location or all of them.
// - Audit log (Administrators only): recent changes from GET api/auditlog/recent,
//   searchable, with each entry's old/new values and a record's full history,
//   and a CSV download of what's listed.
public class ReportsViewModel(IAuthService auth, IRoomService rooms, ReportsApiService reports)
{
    // South Africa is UTC+2 all year; the API stores audit times in UTC.
    private static readonly TimeSpan SouthAfricaOffset = TimeSpan.FromHours(2);

    public bool CanViewReports { get; private set; }
    public bool CanViewAuditLog { get; private set; }
    public bool IsAdministrator { get; private set; }
    public string Tab { get; set; } = "bookings";

    // --- Bookings report ---
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? LocationId { get; set; }
    public List<OfficeLocation> Locations { get; } = [];
    public string? OwnLocationName { get; private set; }
    public ApiBookingStats? Stats { get; private set; }
    public bool IsLoadingStats { get; private set; }
    public bool IsExporting { get; private set; }
    public string? ReportError { get; private set; }

    // --- Audit log ---
    public List<ApiAuditLog> AuditEntries { get; } = [];
    public int AuditCount { get; set; } = 100;
    public string AuditSearch { get; set; } = string.Empty;
    public bool IsLoadingAudit { get; private set; }
    public bool AuditLoaded { get; private set; }
    public string? AuditError { get; private set; }
    public int? ExpandedEntryId { get; set; }
    public string? HistoryTitle { get; private set; }
    public List<ApiAuditLog> History { get; } = [];
    public bool IsLoadingHistory { get; private set; }

    public IEnumerable<ApiAuditLog> FilteredAudit
    {
        get
        {
            var term = AuditSearch.Trim();
            if (term.Length == 0) return AuditEntries;

            return AuditEntries.Where(e =>
                Contains(e.ActingUserName, term)
                || Contains(e.Action, term)
                || Contains(e.EntityName, term)
                || Contains(e.EntityId, term)
                || Contains($"{e.EntityName} #{e.EntityId}", term));
        }
    }

    public async Task InitializeAsync()
    {
        var user = auth.CurrentUser;
        CanViewReports = user is not null && RolePermissions.CanViewReports(user.Role);
        CanViewAuditLog = user is not null && RolePermissions.CanViewAuditLog(user.Role);
        IsAdministrator = user?.Role == UserRole.Administrator;

        if (!CanViewReports) return;

        var allLocations = await rooms.GetLocationsAsync();
        Locations.Clear();
        Locations.AddRange(allLocations);

        if (!IsAdministrator)
        {
            // Centre Manager: fixed to their own location.
            LocationId = user!.LocationId;
            OwnLocationName = allLocations.FirstOrDefault(l => l.Id == user.LocationId)?.Name
                              ?? "Your location";
        }

        await LoadStatsAsync();
    }

    public async Task LoadStatsAsync()
    {
        ReportError = ValidateDates();
        if (ReportError is not null) return;

        IsLoadingStats = true;
        try
        {
            Stats = await reports.GetBookingStatsAsync(ToDateOnly(FromDate), ToDateOnly(ToDate), LocationId);
            if (Stats is null)
                ReportError = "Couldn't load the report. Check that the API is running and you're signed in.";
        }
        finally
        {
            IsLoadingStats = false;
        }
    }

    public async Task<(byte[]? Content, string FileName)> ExportBookingsAsync()
    {
        ReportError = ValidateDates();
        if (ReportError is not null) return (null, string.Empty);

        IsExporting = true;
        try
        {
            var result = await reports.ExportBookingsCsvAsync(ToDateOnly(FromDate), ToDateOnly(ToDate), LocationId);
            if (result.Content is null)
                ReportError = "The download failed. Check that the API is running and you're signed in.";
            return result;
        }
        finally
        {
            IsExporting = false;
        }
    }

    public async Task LoadAuditAsync()
    {
        if (!CanViewAuditLog) return;

        IsLoadingAudit = true;
        AuditError = null;
        try
        {
            var entries = await reports.GetRecentAuditAsync(AuditCount);
            AuditEntries.Clear();
            AuditEntries.AddRange(entries);
            AuditLoaded = true;
        }
        catch (Exception ex)
        {
            AuditError = $"Couldn't load the audit log: {ex.Message}";
        }
        finally
        {
            IsLoadingAudit = false;
        }
    }

    public async Task ShowHistoryAsync(ApiAuditLog entry)
    {
        HistoryTitle = $"{entry.EntityName} #{entry.EntityId}";
        History.Clear();
        IsLoadingHistory = true;
        try
        {
            History.AddRange(await reports.GetRecordHistoryAsync(entry.EntityName, entry.EntityId));
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    public void CloseHistory()
    {
        HistoryTitle = null;
        History.Clear();
    }

    public (byte[] Content, string FileName) BuildAuditCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine("When (SAST),Who,Action,Record,Record id,Old values,New values");

        foreach (var e in FilteredAudit)
        {
            csv.AppendLine(string.Join(",",
                Csv(ToLocal(e.Timestamp).ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(e.ActingUserName ?? $"User {e.ActingUserId}"),
                Csv(e.Action),
                Csv(e.EntityName),
                Csv(e.EntityId),
                Csv(e.OldValues ?? string.Empty),
                Csv(e.NewValues ?? string.Empty)));
        }

        return (Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            $"flexispace-audit-{DateTime.UtcNow.Add(SouthAfricaOffset):yyyyMMdd-HHmm}.csv");
    }

    public static DateTime ToLocal(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Unspecified).Add(SouthAfricaOffset);

    // Field-by-field view of what an audit entry changed: for an update
    // only the fields whose value differs, for a create/delete every field.
    public static IReadOnlyList<(string Field, string? Before, string? After)> DescribeChanges(ApiAuditLog entry)
    {
        var before = ParseObject(entry.OldValues);
        var after = ParseObject(entry.NewValues);

        if (before is null && after is null)
        {
            // Not JSON objects - show whatever was stored.
            var raw = new List<(string, string?, string?)>();
            if (!string.IsNullOrWhiteSpace(entry.OldValues) || !string.IsNullOrWhiteSpace(entry.NewValues))
                raw.Add(("Value", entry.OldValues, entry.NewValues));
            return raw;
        }

        var fields = (before?.Keys ?? Enumerable.Empty<string>())
            .Union(after?.Keys ?? Enumerable.Empty<string>())
            .ToList();

        var rows = new List<(string, string?, string?)>();
        foreach (var field in fields)
        {
            string? oldValue = null, newValue = null;
            before?.TryGetValue(field, out oldValue);
            after?.TryGetValue(field, out newValue);

            if (before is not null && after is not null && oldValue == newValue)
                continue;

            rows.Add((field, oldValue, newValue));
        }

        return rows;
    }

    private static Dictionary<string, string?>? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            return doc.RootElement.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => p.Value.GetRawText()
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string? ValidateDates() =>
        FromDate.HasValue && ToDate.HasValue && FromDate > ToDate
            ? "The 'from' date must be on or before the 'to' date."
            : null;

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
            value = "'" + value;

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
