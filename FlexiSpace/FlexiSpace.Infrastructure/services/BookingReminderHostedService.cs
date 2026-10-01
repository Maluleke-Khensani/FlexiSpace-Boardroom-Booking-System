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
    // Sends booking reminders at three lead times: 24 hours before (in-app +
    // email), 2 hours before (in-app only), and 1 hour before (in-app + email).
    // The two emails are the project plan's "Reminder (24 hrs)" and
    // "Reminder (1 hr)"; both carry the full booking details. This consolidates two reminder systems that were
    // built independently and merged here - see Booking.ReminderSentAt /
    // Reminder24hSentAt / Reminder2hSentAt, one stamp per window, so each
    // window fires exactly once per booking no matter how often this polls.
    //
    // Runs on a simple poll loop rather than scheduling one timer per
    // booking - far simpler, and completely fine at FlexiSpace's scale.
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

        // How often to check for bookings entering a reminder window. A
        // booking is caught on the first poll after it enters a window and
        // immediately marked as reminded for that window, so this only
        // trades off "how late can a reminder be" against DB load - it
        // does not risk a duplicate send. 5 minutes keeps the 2-hour and
        // 1-hour reminders accurate to within 5 minutes, which is fine for
        // a meeting reminder.
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

            // Narrow by date in SQL (DateOnly/TimeOnly can't be combined
            // inside the query itself - same limitation BlockedPeriodService
            // works around), then compare the exact start time in memory.
            // The widest window we care about is 24 hours, so cast the net
            // out to tomorrow + 1 day to be safe around midnight boundaries.
            var today = DateOnly.FromDateTime(nowSouthAfrica);
            var horizon = today.AddDays(2);

            var candidates = await context.Bookings
                .Include(b => b.Boardroom)
                .Include(b => b.User)
                .Where(b => b.Status == BookingStatus.Confirmed
                    && b.BookingDate >= today
                    && b.BookingDate <= horizon
                    && (b.Reminder24hSentAt == null || b.Reminder2hSentAt == null || b.ReminderSentAt == null))
                .ToListAsync(stoppingToken);

            var changed = false;

            foreach (var booking in candidates)
            {
                var startsAt = booking.BookingDate.ToDateTime(booking.StartTime);

                if (startsAt <= nowSouthAfrica)
                {
                    // Already started or passed - nothing left to remind
                    // about. Stamp whatever's still unset so a stale
                    // booking doesn't get re-evaluated on every poll
                    // forever.
                    booking.Reminder24hSentAt ??= DateTime.UtcNow;
                    booking.Reminder2hSentAt ??= DateTime.UtcNow;
                    booking.ReminderSentAt ??= DateTime.UtcNow;
                    changed = true;
                    continue;
                }

                if (booking.Boardroom == null || booking.User == null || !booking.User.IsActive)
                {
                    // Nothing sensible to send - still stamp everything so
                    // a deleted/deactivated user's stale booking doesn't
                    // get re-evaluated on every single poll forever.
                    booking.Reminder24hSentAt ??= DateTime.UtcNow;
                    booking.Reminder2hSentAt ??= DateTime.UtcNow;
                    booking.ReminderSentAt ??= DateTime.UtcNow;
                    changed = true;
                    continue;
                }

                if (booking.Reminder24hSentAt == null && startsAt <= nowSouthAfrica.AddHours(24))
                {
                    await SendInAppReminderAsync(notificationService, booking, "24 hours");

                    // A booking made less than 2 hours ahead is already inside the
                    // 2-hour window too; it gets the 1-hour email shortly, so skip
                    // a second email here that would land at almost the same time.
                    if (startsAt > nowSouthAfrica.AddHours(2))
                    {
                        await SendReminderEmailAsync(
                            emailService,
                            booking,
                            $"Reminder - your booking is tomorrow ({booking.Boardroom.Name})",
                            "This is a reminder that you have a boardroom booking in about 24 hours. Here are the details:");
                    }

                    // Stamped even if the email failed - same reasoning as the
                    // 1-hour reminder below.
                    booking.Reminder24hSentAt = DateTime.UtcNow;
                    changed = true;
                }

                if (booking.Reminder2hSentAt == null && startsAt <= nowSouthAfrica.AddHours(2))
                {
                    await SendInAppReminderAsync(notificationService, booking, "2 hours");
                    booking.Reminder2hSentAt = DateTime.UtcNow;
                    changed = true;
                }

                if (booking.ReminderSentAt == null && startsAt <= nowSouthAfrica.AddHours(1))
                {
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
                            "Failed to create in-app 1-hour reminder notification for booking {BookingId}.",
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
                            "Failed to email 1-hour reminder for booking {BookingId}.",
                            booking.Id);
                    }

                    // Stamped regardless of whether the sends above
                    // actually succeeded - a Graph outage should mean
                    // "this reminder was missed", not "retry every 5
                    // minutes until the meeting is over".
                    booking.ReminderSentAt = DateTime.UtcNow;
                    changed = true;
                }
            }

            if (changed)
            {
                await context.SaveChangesAsync(stoppingToken);
            }
        }

        // Emails a reminder with the full booking details. Never throws: a
        // failed send is logged and the reminder counts as missed.
        private async Task SendReminderEmailAsync(IEmailService emailService, Booking booking, string subject, string leadLine)
        {
            try
            {
                var body = BookingService.BuildBookingDetailsEmailBody(booking, booking.Boardroom!, leadLine);
                await emailService.SendEmailAsync(booking.User!.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to email reminder '{Subject}' for booking {BookingId}.",
                    subject,
                    booking.Id);
            }
        }

        // In-app reminder used by the 24-hour and 2-hour windows (the
        // 1-hour one above builds its own in-app message).
        private async Task SendInAppReminderAsync(INotificationService notificationService, Booking booking, string window)
        {
            try
            {
                await notificationService.CreateNotificationAsync(
                    booking.User!.Id,
                    "Upcoming booking reminder",
                    $"Your booking for {booking.Boardroom!.Name} on {booking.BookingDate:yyyy-MM-dd} at {booking.StartTime:HH\\:mm} is coming up in {window}.",
                    NotificationType.BookingReminder);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to create in-app {Window} reminder notification for booking {BookingId}.",
                    window,
                    booking.Id);
            }
        }
    }
}
