using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Notification;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // No POST or DELETE here on purpose - Notification is system-managed
    // (see data model handover). Rows are created by other services
    // (booking approved/rejected/cancelled, reminders) via
    // INotificationService.CreateNotificationAsync, not by users.
    //
    // Every action scopes to the caller's own notifications via
    // ICurrentUserService - there's no [AuthorizeRoles] here because this
    // isn't a role question, it's "you can only ever see your own inbox".
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;

        public NotificationController(
            INotificationService notificationService,
            ICurrentUserService currentUserService)
        {
            _notificationService = notificationService;
            _currentUserService = currentUserService;
        }

        // Retrieves the current user's notifications, newest first.
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();

            if (userId == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            var notifications = await _notificationService.GetNotificationsForUserAsync(userId.Value);

            return Ok(notifications.Select(MapToResponseDto));
        }

        // Retrieves just the unread count - cheap enough to poll for a
        // notification-bell badge without pulling the whole list.
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();

            if (userId == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            var count = await _notificationService.GetUnreadCountAsync(userId.Value);

            return Ok(new { unreadCount = count });
        }

        // Marks a single notification as read. Scoped to the caller -
        // returns 404 (not 403) if the notification belongs to someone
        // else, so this endpoint doesn't leak whether a given id exists.
        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();

            if (userId == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            var updated = await _notificationService.MarkAsReadAsync(id, userId.Value);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Marks every unread notification for the current user as read.
        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = await _currentUserService.GetCurrentUserIdAsync();

            if (userId == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            var count = await _notificationService.MarkAllAsReadAsync(userId.Value);

            return Ok(new { markedAsRead = count });
        }

        private static NotificationResponseDto MapToResponseDto(Notification notification)
        {
            return new NotificationResponseDto
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                SentAt = notification.SentAt
            };
        }
    }
}
