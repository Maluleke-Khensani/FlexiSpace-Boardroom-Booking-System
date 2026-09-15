using FlexiSpace.Core.DTOs.GroqAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Services
{
    public interface IAiRecommendationService
    {
        Task<AiSuggestionResponseDto> SuggestBoardroomAsync(
            string userNeed,
            DateOnly? bookingDate,
            TimeOnly? startTime,
            TimeOnly? endTime);
    }
}