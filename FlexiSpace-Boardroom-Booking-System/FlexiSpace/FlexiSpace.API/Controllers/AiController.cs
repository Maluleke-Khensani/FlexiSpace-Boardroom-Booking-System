using FlexiSpace.Core.DTOs.GroqAI;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly IAiRecommendationService _aiRecommendationService;

        public AiController(
            IAiRecommendationService aiRecommendationService)
        {
            _aiRecommendationService = aiRecommendationService;
        }

        [HttpPost("suggest")]
        public async Task<ActionResult<AiSuggestionResponseDto>> Suggest(
            [FromBody] AiSuggestionRequestDto request)
        {
            // Make sure the user has provided a description of what
            // they need for their meeting before calling the AI service.
            if (string.IsNullOrWhiteSpace(request.UserNeed))
            {
                return BadRequest(new AiSuggestionResponseDto
                {
                    Success = false,
                    Message = "Please describe what you need for your meeting."
                });
            }

            // Pass the user's meeting requirements together with the
            // requested date and time to the AI recommendation service.
            //
            // The date and time allow the service to first retrieve
            // boardrooms that are available for the requested period.
            // The AI then recommends a suitable room from those
            // available boardrooms.
            var result =
                await _aiRecommendationService
                    .SuggestBoardroomAsync(
                        request.UserNeed,
                        request.BookingDate,
                        request.StartTime,
                        request.EndTime);

            // Return the AI recommendation to the client.
            return Ok(result);
        }
    }
}