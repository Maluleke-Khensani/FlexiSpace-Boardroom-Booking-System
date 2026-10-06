using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GraphTestController : ControllerBase
    {
        private readonly ICalendarService _calendarService;

        public GraphTestController(ICalendarService calendarService)
        {
            _calendarService = calendarService;
        }
/*
        [HttpPost("create-test-event")]
        public async Task<IActionResult> CreateTestEvent()
        {
            var eventId = await _calendarService.CreateCalendarEventAsync(
                "flexispace.test@khensanimaluleke309gmail.onmicrosoft.com",
                "FlexiSpace Graph API Test",
                DateTime.Now.AddMinutes(10),
                DateTime.Now.AddMinutes(40),
                "Test event created by the FlexiSpace Microsoft Graph integration."
        );

            return Ok(new
            {
                message = "Test event created successfully.",
                eventId
            });
        }
*/
    }
}