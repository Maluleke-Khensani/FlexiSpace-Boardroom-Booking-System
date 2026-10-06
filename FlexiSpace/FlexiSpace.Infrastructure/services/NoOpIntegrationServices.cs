using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Services;

namespace FlexiSpace.Infrastructure.services;

/// <summary>
/// Local/dev stand-ins when Graph / Entra app credentials are not configured.
/// </summary>
public sealed class NoOpEmailService : IEmailService
{
    public Task SendBookingConfirmationAsync(string recipientEmail, string recipientName, string boardroomName, string locationName, string locationAddress, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, int numberOfAttendees, string? company, string? notes) =>
        Task.CompletedTask;

    public Task SendBookingModifiedAsync(string recipientEmail, string recipientName, string boardroomName, string locationName, string locationAddress, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, int numberOfAttendees, string? company, string? notes) =>
        Task.CompletedTask;

    public Task SendBookingCancellationAsync(string recipientEmail, string recipientName, string boardroomName, string locationName, string locationAddress, DateOnly bookingDate, TimeOnly startTime, TimeOnly endTime, int numberOfAttendees, string? company, string? notes) =>
        Task.CompletedTask;
}

public sealed class NoOpCalendarService : ICalendarService
{
    public Task<string> CreateCalendarEventAsync(string calendarEmail, string subject, DateTime start, DateTime end, string? description = null) =>
        Task.FromResult(string.Empty);

    public Task UpdateCalendarEventAsync(string calendarEmail, string eventId, string subject, DateTime start, DateTime end, string? description = null) =>
        Task.CompletedTask;

    public Task DeleteCalendarEventAsync(string calendarEmail, string eventId) =>
        Task.CompletedTask;

    public Task<bool> IsCalendarAvailableAsync(string calendarEmail, DateTime start, DateTime end) =>
        Task.FromResult(true);
}

public sealed class NoOpEntraUserService : IEntraUserService
{
    public Task<IEnumerable<EntraUserResponseDto>> GetUsersAsync() =>
        Task.FromResult(Enumerable.Empty<EntraUserResponseDto>());

    public Task<EntraUserResponseDto?> GetUserByIdAsync(Guid entraObjectId) =>
        Task.FromResult<EntraUserResponseDto?>(null);
}
