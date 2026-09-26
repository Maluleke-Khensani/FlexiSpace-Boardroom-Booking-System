using FlexiSpace.API.Authorization;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.DTOs.Catering;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Ownership/role policy (agreed with Tino - CentreManager acts as the
    // "can manage everyone's bookings" role alongside Administrator):
    //
    // - Any authenticated user can view bookings (shared room calendar -
    //   seeing who's booked what is normal for this kind of system, same
    //   as an Outlook room calendar).
    // - Create: a booking is created for yourself. Only a CentreManager or
    //   Administrator may create a booking on behalf of someone else by
    //   supplying a different UserId - anyone else's UserId is ignored and
    //   replaced with their own id, so a Client/Staff caller can't spoof
    //   another user's id in the request body.
    // - Update / Cancel (delete): allowed for the booking's own owner, or
    //   a CentreManager/Administrator acting on someone else's booking.
    //   Anyone else gets 403.
    // - Status changes other than the owner's own cancellation (e.g.
    //   marking a booking Completed) are a front-desk/management action -
    //   CentreManager/Administrator only.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : AuditableControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly INotificationService _notificationService;

        public BookingController(
            IBookingService bookingService,
            IAuditService auditService,
            ICurrentUserService currentUserService,
            INotificationService notificationService)
            : base(auditService, currentUserService)
        {
            _bookingService = bookingService;
            _notificationService = notificationService;
        }

        // Retrieves all bookings.
        [HttpGet]
        public async Task<IActionResult> GetAllBookings()
        {
            var bookings = await _bookingService.GetAllBookingsAsync();

            var response = bookings.Select(MapToResponseDto);

            return Ok(response);
        }

        // Searches bookings with optional filters (boardroom, location, user,
        // status, date range, free-text on Company/Notes), sorted by
        // date/time, and paginated. This is a separate endpoint from
        // GetAllBookings so existing front-end code built against the plain
        // list keeps working unchanged - flag in the group chat once this
        // is ready to be adopted.
        [HttpGet("search")]
        public async Task<IActionResult> SearchBookings([FromQuery] BookingQueryParameters query)
        {
            var result = await _bookingService.SearchBookingsAsync(query);

            var response = new PagedResult<BookingResponseDto>
            {
                Items = result.Items.Select(MapToResponseDto).ToList(),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };

            return Ok(response);
        }

        // Retrieves a specific booking by its ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookingById(int id)
        {
            var booking = await _bookingService.GetBookingByIdAsync(id);

            if (booking == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(booking));
        }

        // Creates a new booking.
        [HttpPost]
        public async Task<IActionResult> CreateBooking(BookingCreateDto dto)
        {
            var currentUser = await CurrentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            // A regular caller can only ever book for themselves. Only a
            // manager may set UserId to someone other than themselves (e.g.
            // a CentreManager booking a room for a visiting client), which
            // is why dto.UserId is trusted only from a manager and ignored
            // otherwise - see class summary above.
            var effectiveUserId = IsManagerRole(currentUser.Role) && dto.UserId != 0
                ? dto.UserId
                : currentUser.Id;

            var booking = new Booking
            {
                BoardroomId = dto.BoardroomId,
                UserId = effectiveUserId,
                BookingDate = dto.BookingDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Company = dto.Company,
                NumberOfAttendees = dto.NumberOfAttendees,
                Notes = dto.Notes
            };

            foreach (var equipment in dto.Equipment)
            {
                booking.BookingEquipments.Add(new BookingEquipment
                {
                    EquipmentId = equipment.EquipmentId,
                    Quantity = equipment.Quantity
                });
            }

            foreach (var catering in dto.Catering)
            {
                booking.BookingCaterings.Add(new BookingCatering
                {
                    CateringId = catering.CateringId,
                    Quantity = catering.Quantity
                });
            }

            try
            {
                var createdBooking = await _bookingService.CreateBookingAsync(booking);

                await LogActionAsync(
                    AuditAction.Create,
                    nameof(Booking),
                    createdBooking.Id.ToString(),
                    newValues: new
                    {
                        createdBooking.BoardroomId,
                        createdBooking.UserId,
                        createdBooking.BookingDate,
                        createdBooking.StartTime,
                        createdBooking.EndTime
                    });

                await _notificationService.CreateNotificationAsync(
                    createdBooking.UserId,
                    "Booking confirmed",
                    $"Your booking for {createdBooking.BookingDate:yyyy-MM-dd} " +
                    $"{createdBooking.StartTime:HH:mm}-{createdBooking.EndTime:HH:mm} has been created.",
                    NotificationType.BookingCreated);

                return CreatedAtAction(
                    nameof(GetBookingById),
                    new { id = createdBooking.Id },
                    MapToResponseDto(createdBooking));
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Updates an existing booking.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBooking(int id, BookingUpdateDto dto)
        {
            var currentUser = await CurrentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            var existingBooking = await _bookingService.GetBookingByIdAsync(id);

            if (existingBooking == null)
            {
                return NotFound();
            }

            // Ownership check: only the booking's own owner, or a manager
            // acting on someone else's booking, may edit it.
            if (existingBooking.UserId != currentUser.Id && !IsManagerRole(currentUser.Role))
            {
                return StatusCode(403, new
                {
                    message = "You can only edit your own bookings.",
                    yourRole = currentUser.Role.ToString()
                });
            }

            var booking = new Booking
            {
                BoardroomId = dto.BoardroomId,
                BookingDate = dto.BookingDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Company = dto.Company,
                NumberOfAttendees = dto.NumberOfAttendees,
                Notes = dto.Notes
            };

            var equipment = dto.Equipment.Select(e => new BookingEquipment
            {
                EquipmentId = e.EquipmentId,
                Quantity = e.Quantity
            }).ToList();

            var catering = dto.Catering.Select(c => new BookingCatering
            {
                CateringId = c.CateringId,
                Quantity = c.Quantity
            }).ToList();

            try
            {
                var updated = await _bookingService.UpdateBookingAsync(
                    id,
                    booking,
                    equipment,
                    catering,
                    modifiedById: currentUser.Id);

                if (!updated)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Update,
                    nameof(Booking),
                    id.ToString(),
                    oldValues: new
                    {
                        existingBooking.BoardroomId,
                        existingBooking.BookingDate,
                        existingBooking.StartTime,
                        existingBooking.EndTime,
                        existingBooking.NumberOfAttendees
                    },
                    newValues: new
                    {
                        dto.BoardroomId,
                        dto.BookingDate,
                        dto.StartTime,
                        dto.EndTime,
                        dto.NumberOfAttendees
                    });

                await _notificationService.CreateNotificationAsync(
                    existingBooking.UserId,
                    "Booking updated",
                    $"Your booking for {dto.BookingDate:yyyy-MM-dd} " +
                    $"{dto.StartTime:HH:mm}-{dto.EndTime:HH:mm} has been changed.",
                    NotificationType.BookingModified);

                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Cancels an existing booking (soft delete - see IBookingService).
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var currentUser = await CurrentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized(new { message = "No active account found for this token." });
            }

            var existingBooking = await _bookingService.GetBookingByIdAsync(id);

            if (existingBooking == null)
            {
                return NotFound();
            }

            // Ownership check: only the booking's own owner, or a manager,
            // may cancel it.
            if (existingBooking.UserId != currentUser.Id && !IsManagerRole(currentUser.Role))
            {
                return StatusCode(403, new
                {
                    message = "You can only cancel your own bookings.",
                    yourRole = currentUser.Role.ToString()
                });
            }

            try
            {
                var deleted = await _bookingService.DeleteBookingAsync(id, cancelledById: currentUser.Id);

                if (!deleted)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Delete,
                    nameof(Booking),
                    id.ToString(),
                    oldValues: new { existingBooking.BookingDate, existingBooking.StartTime, existingBooking.EndTime });

                await _notificationService.CreateNotificationAsync(
                    existingBooking.UserId,
                    "Booking cancelled",
                    $"Your booking for {existingBooking.BookingDate:yyyy-MM-dd} " +
                    $"{existingBooking.StartTime:HH:mm}-{existingBooking.EndTime:HH:mm} has been cancelled.",
                    NotificationType.BookingCancelled);

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Updates the booking status (e.g. marking it Completed, or an
        // administrative cancellation/override). This is a front-desk /
        // management action, distinct from the owner's own cancel button
        // above - restricted to CentreManager/Administrator.
        [AuthorizeRoles(UserRole.CentreManager, UserRole.Administrator)]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(
            int id,
            BookingStatusDto dto)
        {
            var existingBooking = await _bookingService.GetBookingByIdAsync(id);

            if (existingBooking == null)
            {
                return NotFound();
            }

            try
            {
                var updated = await _bookingService.UpdateBookingStatusAsync(
                    id,
                    dto.Status,
                    dto.ApprovedById);

                if (!updated)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Update,
                    nameof(Booking),
                    id.ToString(),
                    oldValues: new { Status = existingBooking.Status.ToString() },
                    newValues: new { Status = dto.Status.ToString(), dto.ApprovedById });

                if (dto.Status == BookingStatus.Cancelled)
                {
                    await _notificationService.CreateNotificationAsync(
                        existingBooking.UserId,
                        "Booking cancelled",
                        $"Your booking for {existingBooking.BookingDate:yyyy-MM-dd} " +
                        $"{existingBooking.StartTime:HH:mm}-{existingBooking.EndTime:HH:mm} has been cancelled.",
                        NotificationType.BookingCancelled);
                }

                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Converts a Booking entity into a BookingResponseDto.
        private static BookingResponseDto MapToResponseDto(Booking booking)
        {
            return new BookingResponseDto
            {
                Id = booking.Id,
                BoardroomId = booking.BoardroomId,
                UserId = booking.UserId,
                BookingDate = booking.BookingDate,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                Status = booking.Status,
                Company = booking.Company,
                NumberOfAttendees = booking.NumberOfAttendees,
                Notes = booking.Notes,
                CreatedAt = booking.CreatedAt,

                Equipment = booking.BookingEquipments.Select(e => new BookingEquipmentDto
                {
                    EquipmentId = e.EquipmentId,
                    Quantity = e.Quantity
                }).ToList(),

                Catering = booking.BookingCaterings.Select(c => new BookingCateringDto
                {
                    CateringId = c.CateringId,
                    Quantity = c.Quantity
                }).ToList()
            };
        }
    }
}
