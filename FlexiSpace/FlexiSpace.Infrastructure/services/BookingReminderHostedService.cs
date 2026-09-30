using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlexiSpace.Infrastructure.Services
{
    // Sends the "1 hour before your booking" email + in-app reminder.
    //
    // Runs on a simple poll loop rather than scheduling one timer per
    // booking - far simpler, and completely fine at FlexiSpace's scale.
    // Every PollInterval, it looks for Confirmed bookings that start
    // within the next hour and haven't had a reminder sent yet
    // (Booking.ReminderSentAt is null), sends the reminder, then stamps
    // ReminderSentAt immediately so a booking is never reminded twice no
    // matter how often this polls or how long a single pass takes.
    //
    // Registered in Program.cs as builder.Services.AddHostedService<
    // BookingReminderHostedService>() - a singleton by hosted-service
    // convention, so it resolves its own scope per pass (IServiceScopeFactory)
    // to safely use the Scoped ApplicationDbContext/IEmailService/
    // INotificationService.
    public class BookingReminderHostedService : BackgroundService
    {
        // South Africa has a single, fixed UTC+2 offset with no daylight
        // saving - same reasoning as BookingService's SouthAfricaUtcOffset.
        private static readonly TimeSpan SouthAfricaUtcOffset = TimeSpan.FromHours(2);

        // How often to check for bookings entering their reminder window.
        // A booking is caught on the first poll after it enters the
        // window and immediately marked as reminded, so this only trades
        // off "how late can a reminder be" against DB load - it does not
        // risk a duplicate send.
        private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BookingReminderHostedService> _logger;

        public BookingReminderHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<BookingReminderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendDueRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // A failed pass (e.g. a transient DB hiccup) must not
                    // kill the background service - just try again next
                    // interval.
                    _logger.LogError(ex, "Booking reminder pass failed.");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected on shutdown.
                }
            }
        }

        private async Task SendDueRemindersAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var nowSouthAfrica = DateTime.UtcNow + SouthAfricaUtcOffset;
            var windowEnd = nowSouthAfrica.AddHours(1);

            // Narrow by date in SQL (DateOnly/TimeOnly can't be combined
            // inside the query itself - same limitation BlockedPeriodService
            // works around), then compare the exact start time in memory.
            var today = DateOnly.FromDateTime(nowSouthAfrica);
            var tomorrow = today.AddDays(1);

            var candidates = await context.Bookings
                .Include(b => b.Boardroom)
                .Include(b => b.User)
                .Where(b => b.Status == BookingStatus.Confirmed
                    && b.ReminderSentAt == null
                    && (b.BookingDate == today || b.BookingDate == tomorrow))
                .ToListAsync(stoppingToken);

            var due = candidates
                .Where(b =>
                {
                    var startsAt = b.BookingDate.ToDateTime(b.StartTime);
                    return startsAt > nowSouthAfrica && startsAt <= windowEnd;
                })
                .ToList();

            foreach (var booking in due)
            {
                if (booking.Boardroom == null || booking.User == null || !booking.User.IsActive)
                {
                    // Nothing sensible to send - still stamp it so a
                    // deleted/deactivated user's stale booking doesn't get
                    // re-evaluated on every single poll forever.
                    booking.ReminderSentAt = DateTime.UtcNow;
                    continue;
                }

                var subject = $"Reminder - your booking starts in an hour ({booking.Boardroom.Name})";

                var emailBody = BookingService.BuildBookingDetailsEmailBody(
                    booking,
                    booking.Boardroom,
                    "This is a reminder that your boardroom booking starts in about an hour. Here are the details:");

                try
                {
                    await notificationService.CreateNotificationAsync(
                        booking.User.Id,
                        subject,
                        $"Your booking for {booking.Boardroom.Name} starts at {booking.StartTime:HH\\:mm} today.",
                        NotificationType.BookingReminder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to create in-app reminder notification for booking {BookingId}.",
                        booking.Id);
                }

                try
                {
                    await emailService.SendEmailAsync(booking.User.Email, subject, emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to email reminder for booking {BookingId}.",
                        booking.Id);
                }

                // Stamped regardless of whether the send above actually
                // succeeded - a Graph outage should mean "this reminder
                // was missed", not "retry every 5 minutes until the
                // meeting is over".
                booking.ReminderSentAt = DateTime.UtcNow;
            }

            if (due.Count > 0)
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
