namespace Flexispace.Mobile.Services.Api;

public sealed class DevTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DevTokenUser? User { get; set; }
}

public sealed class DevTokenUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? LocationId { get; set; }
}

public sealed class ApiUserDto
{
    public int Id { get; set; }
    public Guid EntraObjectId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? LocationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ApiLocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public sealed class ApiBoardroomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = "Available";
    public int LocationId { get; set; }
    public List<ApiBoardroomEquipmentDto> Equipment { get; set; } = [];
}

public sealed class ApiBoardroomEquipmentDto
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; }
    public string? Name { get; set; }
}

public sealed class ApiBookingDto
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
    public string BoardroomName { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "Confirmed";
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ApiCreateBookingDto
{
    public int BoardroomId { get; set; }
    public int UserId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
}

public sealed class ApiUpdateBookingDto
{
    public int BoardroomId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public List<object> Equipment { get; set; } = [];
    public List<object> Catering { get; set; } = [];
}

public sealed class ApiBookingStatusDto
{
    public string Status { get; set; } = "Confirmed";
}

public sealed class ApiBlockedPeriodDto
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
    public string BoardroomName { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}

public sealed class ApiCreateBlockedPeriodDto
{
    public int BoardroomId { get; set; }
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class ApiNotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ApiUnreadCountDto
{
    public int UnreadCount { get; set; }
}

public sealed class ApiAiSuggestRequest
{
    public string UserNeed { get; set; } = string.Empty;
    public DateOnly? BookingDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
}

public sealed class ApiAiSuggestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? SuggestedBoardroomName { get; set; }
}

public sealed class ApiErrorBody
{
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}
