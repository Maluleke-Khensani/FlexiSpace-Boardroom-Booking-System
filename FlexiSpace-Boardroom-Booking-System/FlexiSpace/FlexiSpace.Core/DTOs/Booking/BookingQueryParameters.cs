using FlexiSpace.Core.Enums;

namespace FlexiSpace.Core.DTOs.Booking
{
    // Optional filters + paging for GET /api/booking/search.
    // Every filter is optional; when set, they combine with AND.
    public class BookingQueryParameters
    {
        public int? BoardroomId { get; set; }

        public int? LocationId { get; set; }

        public int? UserId { get; set; }

        public BookingStatus? Status { get; set; }

        public DateOnly? FromDate { get; set; }

        public DateOnly? ToDate { get; set; }

        // Case-insensitive partial match against Company and Notes.
        public string? Search { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
