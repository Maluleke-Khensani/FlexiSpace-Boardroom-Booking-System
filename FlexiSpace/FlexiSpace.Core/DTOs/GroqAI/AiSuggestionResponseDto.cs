using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.DTOs.GroqAI
{

    // This DTO is used to return the AI suggestion for a boardroom name based on the user's need.
    public class AiSuggestionResponseDto
    {
        public bool Success { get; set; }
        public string? SuggestedBoardroomName { get; set; }
        public string? Reason { get; set; }
        public string? Message { get; set; } // shown to user on failure/fallback
    }
}
