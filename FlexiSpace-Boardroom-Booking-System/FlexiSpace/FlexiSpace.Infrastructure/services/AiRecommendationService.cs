using FlexiSpace.Core.DTOs.GroqAI;
using FlexiSpace.Core.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FlexiSpace.Infrastructure.Services
{
    public class AiRecommendationService : IAiRecommendationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IBookingService _bookingService;

        public AiRecommendationService(
            HttpClient httpClient,
            IConfiguration configuration,
            IBookingService bookingService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _bookingService = bookingService;
        }

        public async Task<AiSuggestionResponseDto> SuggestBoardroomAsync(
            string userNeed,
            DateOnly? bookingDate,
            TimeOnly? startTime,
            TimeOnly? endTime)
        {
            // Get the Groq API key from User Secrets.
            // The API key is not stored directly in the source code.
            var apiKey = _configuration["Groq:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new AiSuggestionResponseDto
                {
                    Success = false,
                    Message = "AI recommendations are currently unavailable."
                };
            }

            try
            {
                // The AI should only recommend rooms that the booking
                // system considers available.
                //
                // If a complete date and time were provided, use the
                // BookingService availability check.
                IEnumerable<FlexiSpace.Core.Entities.Boardroom> boardrooms;

                if (bookingDate.HasValue &&
                    startTime.HasValue &&
                    endTime.HasValue)
                {
                    boardrooms = await _bookingService.GetAvailableBoardroomsAsync(
                        bookingDate.Value,
                        startTime.Value,
                        endTime.Value);
                }
                else
                {
                    // If no booking date/time was provided, there is no
                    // specific time slot to check. In this situation,
                    // the AI cannot make a time-specific availability claim.
                    return new AiSuggestionResponseDto
                    {
                        Success = false,
                        Message = "A booking date and time are required for an availability-based recommendation."
                    };
                }

                // Convert the available boardroom information into text
                // that can be provided to the AI.
                var boardroomInformation = string.Join(
                    "\n\n",
                    boardrooms.Select(b =>
                        $"""
                        Boardroom ID: {b.Id}
                        Name: {b.Name}
                        Capacity: {b.Capacity}
                        Status: {b.Status}
                        Equipment: {(b.BoardroomEquipments.Any()
                            ? string.Join(", ", b.BoardroomEquipments.Select(be =>
                                $"{be.Equipment?.Name} - {be.Equipment?.Description} (Quantity: {be.Quantity})"))
                            : "None")}
                        """));

                // If the booking system found no available rooms,
                // there is no reason to call the AI.
                if (!boardrooms.Any())
                {
                    return new AiSuggestionResponseDto
                    {
                        Success = true,
                        SuggestedBoardroomName = null,
                        Reason = "No boardrooms are available for the requested date and time."
                    };
                }

                // Include the requested booking date and time in the
                // information available to the AI.
                var bookingInformation =
                    $"""
                    Requested booking date: {bookingDate.Value}
                    Requested start time: {startTime.Value}
                    Requested end time: {endTime.Value}
                    """;

                // Tell the AI what its role is and provide the actual
                // available boardroom information from FlexiSpace.
                var systemPrompt =
                    """
                    You are the FlexiSpace Smart Room Recommendation Assistant.

                    Your job is to understand what the user needs for their meeting
                    and recommend the most suitable boardroom from the provided
                    FlexiSpace availability information.

                    The user does NOT need to know the available boardrooms.

                    IMPORTANT RULES:
                    1. Only recommend a boardroom that exists in the provided data.
                    2. Never invent a boardroom name or ID.
                    3. Only recommend one of the boardrooms provided.
                    4. Consider the requested number of people.
                    5. Consider requested equipment.
                    6. The provided boardrooms have already passed the backend
                       availability check for the requested date and time.
                    7. Do not recommend a boardroom that is not in the provided data.
                    8. If none of the provided rooms clearly match the user's
                       requirements, return no recommendation.
                    9. Give the boardroom name and explain briefly why it matches.

                    AVAILABLE FLEXISPACE BOARDROOM DATA:
                    """ + "\n\n" + boardroomInformation
                    + "\n\n" + bookingInformation;

                // Configure the Authorization header using the
                // API key stored securely in User Secrets.
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                var requestBody = new
                {
                    model = "openai/gpt-oss-20b",

                    messages = new[]
                    {
                        new
                        {
                            role = "system",

                            // Ask the AI to return structured JSON so that
                            // the API can separate the room name from
                            // the explanation.
                            content = systemPrompt +
                                """

                                Return your answer ONLY as valid JSON using this format:

                                {
                                    "suggestedBoardroomName": "Name of the recommended boardroom",
                                    "reason": "Short explanation of why it is suitable"
                                }

                                If none of the available boardrooms match the
                                user's requirements, use:

                                {
                                    "suggestedBoardroomName": null,
                                    "reason": "Explain why no suitable boardroom was found"
                                }

                                Do not include Markdown.
                                Do not include code fences.
                                """
                        },
                        new
                        {
                            role = "user",
                            content = userNeed
                        }
                    },

                    // A low temperature makes the AI response more
                    // consistent and reduces unnecessary creativity.
                    temperature = 0.1,

                    // Ask Groq to return the response as a JSON object.
                    response_format = new
                    {
                        type = "json_object"
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);

                using var content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

                // Send the user's requirements and the available
                // boardroom information to the Groq AI model.
                var response = await _httpClient.PostAsync(
                    "https://api.groq.com/openai/v1/chat/completions",
                    content);

                // If Groq is unavailable or returns an error, use the
                // fallback response instead of breaking the application.
                if (!response.IsSuccessStatusCode)
                {
                    return new AiSuggestionResponseDto
                    {
                        Success = false,
                        Message = "AI recommendations are currently unavailable."
                    };
                }

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                // Parse the response returned by Groq.
                using var document =
                    JsonDocument.Parse(responseBody);

                // Extract the AI-generated message content.
                var aiResponse =
                    document.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    return new AiSuggestionResponseDto
                    {
                        Success = false,
                        Message = "The AI returned an empty response."
                    };
                }

                // Parse the structured JSON returned by the AI.
                using var aiResult =
                    JsonDocument.Parse(aiResponse);

                // Extract the recommended boardroom name.
                var suggestedBoardroomName =
                    aiResult.RootElement
                        .GetProperty("suggestedBoardroomName")
                        .GetString();

                // Extract the explanation for the recommendation.
                var reason =
                    aiResult.RootElement
                        .GetProperty("reason")
                        .GetString();

                // Return the structured recommendation to the API caller.
                return new AiSuggestionResponseDto
                {
                    Success = true,
                    SuggestedBoardroomName = suggestedBoardroomName,
                    Reason = reason
                };
            }
            catch (Exception)
            {
                // If the AI provider or another part of the process
                // fails, return a safe fallback message instead of
                // allowing the API to crash.
                return new AiSuggestionResponseDto
                {
                    Success = false,
                    Message = "AI recommendations are currently unavailable."
                };
            }
        }
    }
}