namespace Flexispace.Mobile.Services.Real;

// Everything in this file is a 1:1 mirror of a DTO/enum that already
// exists on the real FlexiSpace.API side (FlexiSpace.Core.DTOs / Enums).
// Duplicated here (rather than shared via a project reference) because
// Flexispace.Mobile lives in a completely separate solution
// (Flexispace.Mobile.slnx) from the API (FlexiSpace.slnx) - there's
// nothing to reference. Field names and casing must match the API's JSON
// exactly. Ported from Flexispace.Web's identical file - keep the two in
// sync if the real API's DTOs change.
//
// The API serializes enums as strings (FlexiSpace.API/Program.cs
// registers JsonStringEnumConverter for exactly this reason), so these
// enums are parsed by name, not by ordinal - their declared member order
// doesn't need to match the API's.

public enum ApiUserRole
{
    CentreManager,
    Staff,
    Administrator,
    Client
}

public enum ApiBookingStatus
{
    Confirmed,
    Cancelled,
    Completed
}

public enum ApiBoardroomStatus
{
    Available,
    Maintenance,
    Unavailable
}

public class ApiUser
{
    public int Id { get; set; }
    public Guid EntraObjectId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ApiUserRole Role { get; set; }
    public bool IsActive { get; set; }
    public int? LocationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApiLocation
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class ApiBoardroomEquipment
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; }
}

public class ApiBoardroom
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public ApiBoardroomStatus Status { get; set; }
    public int LocationId { get; set; }
    public List<ApiBoardroomEquipment> Equipment { get; set; } = new();
    public List<int> ComponentBoardroomIds { get; set; } = new();
    public List<int> CombinedIntoBoardroomIds { get; set; } = new();
}

public class ApiEquipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ApiCatering
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ApiBookingEquipment
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class ApiBookingCatering
{
    public int CateringId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class ApiBookingCreateOrUpdate
{
    public int BoardroomId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public List<ApiBookingEquipment> Equipment { get; set; } = new();
    public List<ApiBookingCatering> Catering { get; set; } = new();
}

public class ApiBooking
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
    public int UserId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ApiBookingStatus Status { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedById { get; set; }
    public int? CancelledById { get; set; }
    public string? OutlookEventId { get; set; }
    public List<ApiBookingEquipment> Equipment { get; set; } = new();
    public List<ApiBookingCatering> Catering { get; set; } = new();
}

public class ApiBlockedPeriod
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ApiBlockedPeriodCreate
{
    public int BoardroomId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public required string Reason { get; set; }
}

public class ApiNotification
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SentAt { get; set; }
}

// Shape of GET api/booking/search - one page of results, not a bare array.
public class ApiPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ApiBookingCountBreakdown
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ApiBookingStats
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int TotalBookings { get; set; }
    public double AverageAttendees { get; set; }
    public List<ApiBookingCountBreakdown> ByStatus { get; set; } = new();
    public List<ApiBookingCountBreakdown> ByLocation { get; set; } = new();
    public List<ApiBookingCountBreakdown> ByBoardroom { get; set; } = new();
}

// Generic envelope for the handful of endpoints that return a plain
// { message: "..." } body on error (BadRequest/NotFound with a string,
// or the RBAC filter's structured 401/403 bodies).
public class ApiErrorBody
{
    public string? Message { get; set; }
    public string? Error { get; set; }
}
