using Flexispace.Core.Helpers;
using Flexispace.Core.Models;
using Flexispace.Core.Services;

namespace Flexispace.Web.Api;

internal static class CatalogMapper
{
    public static OfficeLocation ToLocation(LocationDto dto)
    {
        var seed = MatchLocation(dto.Name);
        return new OfficeLocation
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            Address = dto.Address,
            Phone = seed?.Phone ?? string.Empty,
            CentreManager = seed?.CentreManager ?? string.Empty,
            ManagerEmail = seed?.ManagerEmail ?? string.Empty,
            ImageKey = seed?.ImageKey ?? GuessLocationImage(dto.Name),
            Tagline = seed?.Tagline ?? dto.Address
        };
    }

    public static Boardroom ToRoom(BoardroomDto dto, IReadOnlyDictionary<int, string> equipmentNames)
    {
        var seed = MatchRoom(dto.Name);
        var names = dto.Equipment
            .Select(e => equipmentNames.TryGetValue(e.EquipmentId, out var name) ? name : null)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Cast<string>()
            .ToList();

        return new Boardroom
        {
            Id = dto.Id.ToString(),
            Name = dto.Name,
            LocationId = dto.LocationId.ToString(),
            Capacity = dto.Capacity,
            Equipment = names.Count > 0 ? names : seed?.Equipment ?? [],
            Status = dto.Status switch
            {
                1 => RoomStatus.Maintenance,
                2 => RoomStatus.Blocked,
                _ => RoomStatus.Available
            },
            ImageKey = seed?.ImageKey ?? "room_meeting"
        };
    }

    public static void AttachCombinations(List<Boardroom> rooms)
    {
        var thi = rooms.FirstOrDefault(r => r.Name.Equals("Thingamajik", StringComparison.OrdinalIgnoreCase));
        var wha = rooms.FirstOrDefault(r => r.Name.Equals("Whachamacallit", StringComparison.OrdinalIgnoreCase));
        if (thi is null || wha is null || thi.LocationId != wha.LocationId)
        {
            RoomCombinations.SetCatalog(rooms);
            return;
        }

        var existingCombined = rooms.FirstOrDefault(r =>
            r != thi &&
            r != wha &&
            r.Name.Contains("Thingamajik", StringComparison.OrdinalIgnoreCase) &&
            r.Name.Contains("Whachamacallit", StringComparison.OrdinalIgnoreCase));

        if (existingCombined is not null)
        {
            existingCombined.CombinedRoomIds = [thi.Id, wha.Id];
            existingCombined.Capacity = Math.Max(existingCombined.Capacity, thi.Capacity + wha.Capacity);
        }
        else if (!rooms.Any(r => r.IsCombined))
        {
            rooms.Add(new Boardroom
            {
                Id = $"c-{thi.Id}-{wha.Id}",
                Name = "Thingamajik + Whachamacallit",
                LocationId = thi.LocationId,
                Capacity = thi.Capacity + wha.Capacity,
                Equipment = thi.Equipment.Union(wha.Equipment, StringComparer.OrdinalIgnoreCase).ToList(),
                ImageKey = thi.ImageKey,
                CombinedRoomIds = [thi.Id, wha.Id]
            });
        }

        RoomCombinations.SetCatalog(rooms);
    }

    public static User ToUser(UserDto dto) => new()
    {
        Id = IdMap.ToGuid(dto.Id),
        ApiId = dto.Id,
        Name = $"{dto.FirstName} {dto.LastName}".Trim(),
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        Email = dto.Email,
        Role = dto.Role switch
        {
            0 => UserRole.CentreManager,
            1 => UserRole.Staff,
            2 => UserRole.Administrator,
            3 => UserRole.Client,
            _ => UserRole.Staff
        },
        LocationId = dto.LocationId?.ToString(),
        IsActive = dto.IsActive,
        CreatedAt = dto.CreatedAt,
        EntraObjectId = dto.EntraObjectId
    };

    public static int ToApiRole(UserRole role) => role switch
    {
        UserRole.CentreManager => 0,
        UserRole.Staff => 1,
        UserRole.Administrator => 2,
        UserRole.Client => 3,
        _ => 1
    };

    public static BookingStatus ToWebStatus(int apiStatus) => apiStatus switch
    {
        1 => BookingStatus.Cancelled,
        2 => BookingStatus.Completed,
        _ => BookingStatus.Confirmed
    };

    public static Booking ToBooking(
        BookingDto dto,
        Boardroom? room,
        OfficeLocation? location,
        string bookerName,
        IReadOnlyDictionary<int, string> equipmentNames,
        IReadOnlyDictionary<int, string> cateringNames)
    {
        var start = dto.BookingDate.ToDateTime(dto.StartTime);
        var end = dto.BookingDate.ToDateTime(dto.EndTime);
        return new Booking
        {
            Id = IdMap.ToGuid(dto.Id),
            RoomId = dto.BoardroomId.ToString(),
            LocationId = room?.LocationId ?? location?.Id ?? string.Empty,
            RoomName = room?.Name ?? $"Room {dto.BoardroomId}",
            LocationName = location?.Name ?? room?.LocationId ?? string.Empty,
            Start = start,
            End = end,
            BookerId = IdMap.ToGuid(dto.UserId),
            BookerName = bookerName,
            Company = dto.Company ?? string.Empty,
            Attendees = dto.NumberOfAttendees,
            Equipment = dto.Equipment
                .Select(e => equipmentNames.TryGetValue(e.EquipmentId, out var name) ? name : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .ToList(),
            Catering = dto.Catering
                .Select(c => cateringNames.TryGetValue(c.CateringId, out var name) ? name : null)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Cast<string>()
                .ToList(),
            Notes = dto.Notes ?? string.Empty,
            Status = ToWebStatus(dto.Status)
        };
    }

    public static AppNotification ToNotification(NotificationDto dto) => new()
    {
        Id = IdMap.ToGuid(dto.Id),
        Title = dto.Title,
        Message = dto.Message,
        CreatedAt = dto.CreatedAt.Kind == DateTimeKind.Utc ? dto.CreatedAt.ToLocalTime() : dto.CreatedAt,
        IsRead = dto.IsRead,
        Type = dto.Type switch
        {
            1 => "Cancellation",
            2 => "Reminder",
            3 => "Info",
            _ => "Confirmation"
        }
    };

    private static OfficeLocation? MatchLocation(string name) =>
        SeedData.Locations.FirstOrDefault(l =>
            l.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            name.Contains(l.Name, StringComparison.OrdinalIgnoreCase) ||
            l.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

    private static Boardroom? MatchRoom(string name) =>
        SeedData.Rooms.FirstOrDefault(r =>
            !r.IsCombined && r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string GuessLocationImage(string name)
    {
        if (name.Contains("Centurion", StringComparison.OrdinalIgnoreCase)) return "loc_centurion.jpg";
        if (name.Contains("Houghton", StringComparison.OrdinalIgnoreCase)) return "loc_houghton.jpg";
        if (name.Contains("Eagle", StringComparison.OrdinalIgnoreCase)) return "loc_eagle.jpg";
        return "loc_centurion.jpg";
    }
}
