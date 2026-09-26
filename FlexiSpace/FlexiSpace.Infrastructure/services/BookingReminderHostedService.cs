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
    // Periodically scans for bookings starting soon and sends the 24h/2h
    // reminder push. This is the one notification trigger that isn't a
    // side effect of a controller action - nothing "happens" when a
    // reminder is due, time just passes - so it has to be driven by a
    // timer rather than a request, unlike BookingCreated/Modified/
    // Cancelled which fire straight from BookingController.
    //
    // NOTE on time zones: BookingDate/StartTime are stored as naive
    // DateOnly/TimeOnly with no time zone attached, and the rest of the
    // app (booking-conflict checks in BookingService) already compares
    // them directly with no time zone conversion. This class follows the
    // same assumption - the API server's local clock matches the wall-
    // clock time bookings are made in. If the API is ever deployed
    // somewhere whose local time zone differs from where FlexiSpace
    // actually operates, this (and the conflict-detection logic it
    // mirrors) will need real time zone handling - worth raising with
    // Khensani/Tino if that becomes a real deployment, not just a
    // classroom one.
    public class BookingReminderHostedService : BackgroundService
    {
        // How often to check for bookings needing a reminder. 5 minutes
        // keeps the "2 hours before" reminder accurate to within 5
        // minutes - fine for a meeting reminder - without hammering the
        // database. "Due but not yet sent" (rather than an exact time
        // window) is what's actually checked, so a missed poll (e.g. the
        // app was restarting) still catches up on the next one instead of
        // silently skipping a reminder.
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
                    // One failed scan must not stop future scans - log and
                    // try again next interval.
                    _logger.LogError(ex, "Booking reminder scan failed.");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected during shutdown.
                }
            }
        }

        private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
        {
            // BackgroundService runs for the app's whole lifetime, so it
            // gets its own DI scope per scan rather than holding one
            // DbContext open the entire time.
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var now = DateTime.Now;

            // Cast a wide net in SQL (cheap - indexed-ish range on
            // BookingDate, only Pending bookings, only ones still missing
            // at least one reminder), then do the exact "is this actually
            // within 24h/2h" check in memory, since combining DateOnly +
            // TimeOnly isn't something every EF provider can translate.
            var horizon = DateOnly.FromDateTime(now.AddDays(2));

            var candidates = await context.Bookings
                .Where(b => b.Status == BookingStatus.Pending)
                .Where(b => b.BookingDate <= horizon)
                .Where(b => b.Reminder24hSentAt == null || b.Reminder2hSentAt == null)
                .ToListAsync(cancellationToken);

            var sentAny = false;

            foreach (var booking in candidates)
            {
                var bookingStart = booking.BookingDate.ToDateTime(booking.StartTime);

                if (bookingStart <= now)
                {
                    // Already started or passed - nothing left to remind
                    // about (also protects against ever reminding about a
                    // booking whose date somehow ended up in the past).
                    continue;
                }

                if (booking.Reminder24hSentAt == null && bookingStart <= now.AddHours(24))
                {
                    await SendReminderAsync(notificationService, booking, "24 hours");
                    booking.Reminder24hSentAt = DateTime.UtcNow;
                    sentAny = true;
                }

                if (booking.Reminder2hSentAt == null && bookingStart <= now.AddHours(2))
                {
                    await SendReminderAsync(notificationService, booking, "2 hours");
                    booking.Reminder2hSentAt = DateTime.UtcNow;
                    sentAny = true;
                }
            }

            if (sentAny)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static async Task SendReminderAsync(
            INotificationService notificationService,
            Booking booking,
            string window)
        {
            await notificationService.CreateNotificationAsync(
                booking.UserId,
                "Upcoming booking reminder",
                $"Your booking on {booking.BookingDate:yyyy-MM-dd} at {booking.StartTime:HH:mm} is coming up in {window}.",
                NotificationType.BookingReminder);
        }
    }
}
