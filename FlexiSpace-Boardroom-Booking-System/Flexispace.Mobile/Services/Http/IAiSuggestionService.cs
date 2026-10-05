using System.Net.Http.Json;
using Flexispace.Mobile.Services.Api;

namespace Flexispace.Mobile.Services.Http;

public interface IAiSuggestionService
{
    Task<ApiAiSuggestResponse> SuggestAsync(string userNeed);
}

public sealed class HttpAiSuggestionService(ApiClient api) : IAiSuggestionService
{
    public async Task<ApiAiSuggestResponse> SuggestAsync(string userNeed)
    {
        try
        {
            using var response = await api.PostAsJsonAsync("api/Ai/suggest", new ApiAiSuggestRequest
            {
                UserNeed = userNeed,
                BookingDate = DateOnly.FromDateTime(DateTime.Today),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 0)
            });

            if (!response.IsSuccessStatusCode)
            {
                return new ApiAiSuggestResponse
                {
                    Success = false,
                    Message = "AI suggestions are unavailable right now. Use the FAQ tips below."
                };
            }

            var dto = await response.Content.ReadFromJsonAsync<ApiAiSuggestResponse>(ApiClient.JsonOptions);
            if (dto is null)
                return new ApiAiSuggestResponse { Success = false, Message = "No suggestion returned." };
            if (dto.Success && string.IsNullOrWhiteSpace(dto.Message) && !string.IsNullOrWhiteSpace(dto.Reason))
                dto.Message = dto.Reason;
            return dto;
        }
        catch
        {
            return new ApiAiSuggestResponse
            {
                Success = false,
                Message = "Could not reach the AI service. Check that the API is running."
            };
        }
    }
}

public sealed class NullAiSuggestionService : IAiSuggestionService
{
    public Task<ApiAiSuggestResponse> SuggestAsync(string userNeed) =>
        Task.FromResult(new ApiAiSuggestResponse
        {
            Success = false,
            Message = "AI suggestions are only available when connected to the live API."
        });
}
