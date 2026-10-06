using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GraphEmailTestController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public GraphEmailTestController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("send-test")]
        public async Task<IActionResult> SendTestEmail()
        {
            await _emailService.SendBookingConfirmationAsync(
                recipientEmail: "ST10451309@myemeris.edu.za",
                recipientName: "Test User",
                boardroomName: "Test Boardroom",
                locationName: "Test Location",
                locationAddress: "123 Test Street",
                bookingDate: DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                startTime: new TimeOnly(10, 0),
                endTime: new TimeOnly(11, 0),
                numberOfAttendees: 5,
                company: "Test Company",
                notes: "This is a test email from FlexiSpace."
            );

            return Ok(new { message = "Test email sent successfully." });
        }
    }
}