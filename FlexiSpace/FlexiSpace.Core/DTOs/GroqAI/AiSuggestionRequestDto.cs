using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.DTOs.GroqAI
{
    // This DTO is used to send the user's need to the AI for generating a boardroom name suggestion.
    public class AiSuggestionRequestDto
    {
        // The user's natural-language description of what they need.
        // The user does not need to know the available boardrooms.
        public required string UserNeed { get; set; }

        // Optional date and time information.
        // These allow the backend to check whether a suitable
        // boardroom is actually available.
        public DateOnly? BookingDate { get; set; }

        public TimeOnly? StartTime { get; set; }

        public TimeOnly? EndTime { get; set; }
    }
}