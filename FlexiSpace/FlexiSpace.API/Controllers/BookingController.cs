using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.DTOs.Catering;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // TEMP — local Swagger testing only. Revert before committing.
    // [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
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
        // caller - the request body has no UserId field, so nobody can
        // book as someone else.
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
        // - enforced server-side in BookingService, not just in the apps.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBooking(int id, BookingUpdateDto dto)
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
                    catering);

                if (!updated)
                {
                    return NotFound();
                }

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
            try
            {
                var deleted = await _bookingService.DeleteBookingAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

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

        // Updates the booking status. Only the booking's own owner, a
        // Centre Manager at that location, or an Administrator may change it.
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(
            int id,
            BookingStatusDto dto)
        {
            try
            {
                var updated = await _bookingService.UpdateBookingStatusAsync(
                    id,
                    dto.Status);

                if (!updated)
                {
                    return NotFound();
                }

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