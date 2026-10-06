using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flexispace.Web.Api;

internal static class IdMap
{
    public static Guid ToGuid(int id)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(id).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    public static int ToInt(Guid guid)
    {
        var bytes = guid.ToByteArray();
        return BitConverter.ToInt32(bytes, 0);
    }

    public static bool TryToInt(string value, out int id) =>
        int.TryParse(value, out id);
}

internal sealed class LocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

internal sealed class BoardroomDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Status { get; set; }
    public int LocationId { get; set; }
    public List<BoardroomEquipmentDto> Equipment { get; set; } = [];
}

internal sealed class BoardroomEquipmentDto
{
    public int EquipmentId { get; set; }
    public int Quantity { get; set; }
}

internal sealed class EquipmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

internal sealed class CateringDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class UserDto
{
    public int Id { get; set; }
    public Guid EntraObjectId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Role { get; set; }
    public bool IsActive { get; set; }
    public int? LocationId { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class BookingDto
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
    public int UserId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Status { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BookingItemDto> Equipment { get; set; } = [];
    public List<BookingItemDto> Catering { get; set; } = [];
}

internal sealed class BookingItemDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int CateringId { get; set; }

    public int Quantity { get; set; } = 1;
}

internal sealed class BookingCreatePayload
{
    public int BoardroomId { get; set; }
    public int UserId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public List<BookingItemDto> Equipment { get; set; } = [];
    public List<BookingItemDto> Catering { get; set; } = [];
}

internal sealed class BookingUpdatePayload
{
    public int BoardroomId { get; set; }
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Company { get; set; }
    public int NumberOfAttendees { get; set; }
    public string? Notes { get; set; }
    public List<BookingItemDto> Equipment { get; set; } = [];
    public List<BookingItemDto> Catering { get; set; } = [];
}

internal sealed class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class UnreadCountDto
{
    public int UnreadCount { get; set; }
}

internal sealed class ApiErrorDto
{
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
}

internal sealed class BoardroomCreatePayload
{
    public required string Name { get; set; }
    public int Capacity { get; set; }
    public int LocationId { get; set; }
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Status { get; set; }
    public List<BoardroomEquipmentDto> Equipment { get; set; } = [];
}

internal sealed class BoardroomUpdatePayload
{
    public required string Name { get; set; }
    public int Capacity { get; set; }
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Status { get; set; }
    public int LocationId { get; set; }
    public List<BoardroomEquipmentDto> Equipment { get; set; } = [];
}

internal sealed class UserCreatePayload
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Role { get; set; }
    public int? LocationId { get; set; }
    public Guid? EntraObjectId { get; set; }
}

internal sealed class UserProvisionPayload
{
    public Guid EntraObjectId { get; set; }
    public int? LocationId { get; set; }
}

internal sealed class UserStatusPayload
{
    public bool IsActive { get; set; }
}

internal sealed class UserCreateResultDto
{
    public UserDto? User { get; set; }
    public string? TemporaryPassword { get; set; }
    public bool InvitationSent { get; set; }
    public string? Message { get; set; }
}

internal sealed class UserUpdatePayload
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    [JsonConverter(typeof(ApiEnumIntConverter))]
    public int Role { get; set; }
    public int? LocationId { get; set; }
}

internal sealed class OccupiedSlotDto
{
    public JsonElement StartTime { get; set; }
    public JsonElement EndTime { get; set; }
}

internal sealed class EntraUserDto
{
    public Guid EntraObjectId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

internal sealed class BlockedPeriodDto
{
    public int Id { get; set; }
    public int BoardroomId { get; set; }
}
