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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Ownership/role policy: BookingService itself now enforces who may
    // act on a booking (see CanManageBooking / ApplyVisibilityScope there)
    // and throws ForbiddenException when the caller isn't allowed to -
    // caught below as 403. The controller no longer duplicates that check:
    // - Any authenticated user can view bookings they're allowed to see
    //   (Administrators: all, Centre Managers: their own location,
    //   everyone else: only their own bookings).
    // - Create: the booker is always the authenticated caller - there is
    //   no way to book on behalf of someone else (BookingCreateDto has no
    //   UserId field).
    // - Update / Cancel / change status: the booking's own owner, a
    //   Centre Manager at that boardroom's location, or an Administrator.
    // - Notifications: BookingService tells the booker (in-app + email) on
    //   create/update/cancel. The controller used to send its own extra
    //   in-app notification as well, so every booker got two.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : AuditableControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(
            IBookingService bookingService,
            IAuditService auditService,
            ICurrentUserService currentUserService)
            : base(auditService, currentUserService)
        {
            _bookingService = bookingService;
        }

        // Retrieves all bookings the current caller is allowed to see.
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

        // Creates a new booking. The booker is always the authenticated
        // caller - CreateBookingAsync resolves it internally and ignores
        // anything on the passed-in entity, so there's nothing to set
        // here.
        [HttpPost]
        public async Task<IActionResult> CreateBooking(BookingCreateDto dto)
        {
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

                return CreatedAtAction(
                    nameof(GetBookingById),
                    new { id = createdBooking.Id },
                    MapToResponseDto(createdBooking));
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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

        // Updates an existing booking. Only the booking's own owner, a
        // Centre Manager at that location, or an Administrator may edit it
        // - enforced server-side in BookingService via ForbiddenException.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBooking(int id, BookingUpdateDto dto)
        {
            var existingBooking = await _bookingService.GetBookingByIdAsync(id);

            if (existingBooking == null)
            {
                return NotFound();
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
                var updated = await _bookingService.UpdateBookingAsync(id, booking, equipment, catering);

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

                return NoContent();
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
        // Only the booking's own owner, a Centre Manager at that location,
        // or an Administrator may cancel it.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var existingBooking = await _bookingService.GetBookingByIdAsync(id);

            if (existingBooking == null)
            {
                return NotFound();
            }

            try
            {
                var deleted = await _bookingService.DeleteBookingAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Delete,
                    nameof(Booking),
                    id.ToString(),
                    oldValues: new { existingBooking.BookingDate, existingBooking.StartTime, existingBooking.EndTime });

                return NoContent();
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Updates the booking status (Cancel, mark Completed). Only the
        // booking's own owner, a Centre Manager at that location, or an
        // Administrator may change it - enforced server-side in
        // BookingService, not by a role attribute here, since an owner
        // who isn't a manager is still allowed to do this.
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
                    dto.Status);

                if (!updated)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Update,
                    nameof(Booking),
                    id.ToString(),
                    oldValues: new { Status = existingBooking.Status.ToString() },
                    newValues: new { Status = dto.Status.ToString() });

                return NoContent();
            }
            catch (ForbiddenException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
                ModifiedAt = booking.ModifiedAt,
                ModifiedById = booking.ModifiedById,
                CancelledById = booking.CancelledById,
                OutlookEventId = booking.OutlookEventId,

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
