using FlexiSpace.Core.DTOs.Reporting;

namespace FlexiSpace.Core.Services
{
    // Reporting/export logic (see roles doc - stats, PDF/CSV exports).
    // Both methods share the same optional date-range/location filters so
    // "export exactly the range I just looked at the stats for" is a
    // direct, obvious operation for the front end to offer.
    public interface IReportingService
    {
        Task<BookingStatsResponseDto> GetBookingStatsAsync(
            DateOnly? fromDate,
            DateOnly? toDate,
            int? locationId);

        // Returns a UTF-8 CSV file as raw bytes, ready to hand back as a
        // file download (see ReportingController).
        Task<byte[]> ExportBookingsCsvAsync(
            DateOnly? fromDate,
            DateOnly? toDate,
            int? locationId);
    }
}
