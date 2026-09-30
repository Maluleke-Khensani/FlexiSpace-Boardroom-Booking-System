using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Reporting/export logic (see roles doc). CentreManager as well as
    // Administrator can view this - a branch manager needs their own
    // location's numbers day to day, not just head-office Admins.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [AuthorizeRoles(UserRole.CentreManager, UserRole.Administrator)]
    public class ReportingController : ControllerBase
    {
        private readonly IReportingService _reportingService;

        public ReportingController(IReportingService reportingService)
        {
            _reportingService = reportingService;
        }

        // Aggregate booking stats (totals, by status/location/boardroom,
        // average attendees) for the admin/manager dashboard.
        [HttpGet("booking-stats")]
        public async Task<IActionResult> GetBookingStats(
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] int? locationId)
        {
            var stats = await _reportingService.GetBookingStatsAsync(fromDate, toDate, locationId);

            return Ok(stats);
        }

        // Same filters as GetBookingStats, but returns the underlying
        // bookings as a downloadable CSV file rather than an aggregate.
        [HttpGet("bookings/export")]
        public async Task<IActionResult> ExportBookings(
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] int? locationId)
        {
            var csvBytes = await _reportingService.ExportBookingsCsvAsync(fromDate, toDate, locationId);

            var fileName = $"flexispace-bookings-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            return File(csvBytes, "text/csv", fileName);
        }
    }
}
