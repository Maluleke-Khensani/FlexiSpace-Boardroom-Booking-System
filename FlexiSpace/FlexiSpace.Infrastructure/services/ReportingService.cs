using System.Text;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Reporting;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class ReportingService : IReportingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ReportingService(ApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<BookingStatsResponseDto> GetBookingStatsAsync(
            DateOnly? fromDate,
            DateOnly? toDate,
            int? locationId)
        {
            var query = await FilteredBookingsQueryAsync(fromDate, toDate, locationId);

            var bookings = await query
                .Select(b => new
                {
                    b.Status,
                    b.NumberOfAttendees,
                    BoardroomName = b.Boardroom!.Name,
                    LocationId = b.Boardroom!.LocationId,
                    LocationName = b.Boardroom!.Location!.Name
                })
                .ToListAsync();

            return new BookingStatsResponseDto
            {
                FromDate = fromDate,
                ToDate = toDate,
                TotalBookings = bookings.Count,

                // Guard against dividing by zero when the filtered range
                // has no bookings at all.
                AverageAttendees = bookings.Count == 0
                    ? 0
                    : bookings.Average(b => b.NumberOfAttendees),

                ByStatus = bookings
                    .GroupBy(b => b.Status.ToString())
                    .Select(g => new BookingCountBreakdownDto { Label = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList(),

                ByLocation = bookings
                    .GroupBy(b => b.LocationName)
                    .Select(g => new BookingCountBreakdownDto { Label = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList(),

                ByBoardroom = bookings
                    .GroupBy(b => b.BoardroomName)
                    .Select(g => new BookingCountBreakdownDto { Label = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList()
            };
        }

        public async Task<byte[]> ExportBookingsCsvAsync(
            DateOnly? fromDate,
            DateOnly? toDate,
            int? locationId)
        {
            var query = await FilteredBookingsQueryAsync(fromDate, toDate, locationId);

            var bookings = await query
                .OrderBy(b => b.BookingDate)
                .ThenBy(b => b.StartTime)
                .Select(b => new
                {
                    b.Id,
                    BoardroomName = b.Boardroom!.Name,
                    LocationName = b.Boardroom!.Location!.Name,
                    UserName = b.User == null ? "" : b.User.FirstName + " " + b.User.LastName,
                    UserEmail = b.User == null ? "" : b.User.Email,
                    b.BookingDate,
                    b.StartTime,
                    b.EndTime,
                    b.Status,
                    b.Company,
                    b.NumberOfAttendees
                })
                .ToListAsync();

            var csv = new StringBuilder();

            csv.AppendLine(
                "BookingId,Boardroom,Location,BookedBy,Email,Date,StartTime,EndTime,Status,Company,Attendees");

            foreach (var b in bookings)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    b.Id.ToString(),
                    CsvEscape(b.BoardroomName),
                    CsvEscape(b.LocationName),
                    CsvEscape(b.UserName),
                    CsvEscape(b.UserEmail),
                    b.BookingDate.ToString("yyyy-MM-dd"),
                    b.StartTime.ToString("HH:mm"),
                    b.EndTime.ToString("HH:mm"),
                    b.Status.ToString(),
                    CsvEscape(b.Company ?? ""),
                    b.NumberOfAttendees.ToString()
                }));
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        // A Centre Manager only ever gets their own location's numbers,
        // whatever locationId they asked for - the same rule booking
        // visibility uses. Administrators can pick any location, or all.
        private async Task<IQueryable<Booking>> FilteredBookingsQueryAsync(
            DateOnly? fromDate,
            DateOnly? toDate,
            int? locationId)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser?.Role == UserRole.CentreManager)
            {
                // No assigned location -> nothing to report on.
                locationId = currentUser.LocationId ?? -1;
            }

            var query = _context.Bookings
                .Include(b => b.Boardroom)
                    .ThenInclude(bo => bo!.Location)
                .Include(b => b.User)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(b => b.BookingDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(b => b.BookingDate <= toDate.Value);
            }

            if (locationId.HasValue)
            {
                query = query.Where(b => b.Boardroom!.LocationId == locationId.Value);
            }

            return query;
        }

        // Wraps a field in quotes and escapes embedded quotes if it
        // contains a comma, quote, or newline - the minimum needed for a
        // spec-correct CSV field (RFC 4180) given names/notes are
        // free-text and can contain commas.
        private static string CsvEscape(string value)
        {
            // A cell starting with = + - or @ is run as a formula when the
            // file is opened in Excel - prefix it so it's shown as text.
            if (value.Length > 0 && "=+-@".Contains(value[0]))
            {
                value = "'" + value;
            }

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
