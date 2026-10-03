using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.DTOs.Catering;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Enums;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IBoardroomService _boardroomService;

        public BookingController(IBookingService bookingService,
            ICurrentUserService currentUserService,
            IBoardroomService boardroomService)
        {
            _bookingService = bookingService;
            _currentUserService = currentUserService;
            _boardroomService = boardroomService;
        }

        // Retrieves all bookings.
        [HttpGet]
        public async Task<IActionResult> GetAllBookings()
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
                return Unauthorized();

            IEnumerable<Core.Entities.Booking> bookings;
            if (currentUser.Role == UserRole.Administrator)
            {
                bookings = await _bookingService.GetAllBookingsAsync();
            }
            else if (currentUser.Role == UserRole.CentreManager)
            {
                // Centre managers see bookings for their location.
                bookings = (await _bookingService.GetAllBookingsAsync())
                    .Where(b => b.Boardroom != null && b.Boardroom.LocationId == currentUser.LocationId);
            }
            else
            {
                // Other non-admin users should only see their own bookings.
                bookings = (await _bookingService.GetAllBookingsAsync())
                    .Where(b => b.UserId == currentUser.Id);
            }

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
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
                return Unauthorized();

            if (currentUser.Role == UserRole.CentreManager)
            {
                // Centre managers can search bookings for their location only.
                query.LocationId = currentUser.LocationId;
            }
            else if (currentUser.Role != UserRole.Administrator)
            {
                // Restrict other non-admin users to their own bookings only.
                query.UserId = currentUser.Id;
            }

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

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
                return Unauthorized();
            if (currentUser.Role == UserRole.Administrator)
            {
                return Ok(MapToResponseDto(booking));
            }

            // Centre managers can view bookings for their location
            if (currentUser.Role == UserRole.CentreManager)
            {
                if (booking.Boardroom != null && booking.Boardroom.LocationId == currentUser.LocationId)
                {
                    return Ok(MapToResponseDto(booking));
                }

                return Forbid();
            }

            // Other users may only view their own bookings
            if (booking.UserId == currentUser.Id)
            {
                return Ok(MapToResponseDto(booking));
            }

            return Forbid();
        }

        // Creates a new booking.
        [HttpPost]
        public async Task<IActionResult> CreateBooking(BookingCreateDto dto)
        {
            var booking = new Booking
            {
                BoardroomId = dto.BoardroomId,
                UserId = dto.UserId,
                BookingDate = dto.BookingDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Company = dto.Company,
                NumberOfAttendees = dto.NumberOfAttendees,
                Notes = dto.Notes
            };

            if (dto.Equipment != null)
            {
                foreach (var equipment in dto.Equipment)
                {
                    booking.BookingEquipments.Add(new BookingEquipment
                    {
                        EquipmentId = equipment.EquipmentId,
                        Quantity = equipment.Quantity
                    });
                }
            }

            if (dto.Catering != null)
            {
                foreach (var catering in dto.Catering)
                {
                    booking.BookingCaterings.Add(new BookingCatering
                    {
                        CateringId = catering.CateringId,
                        Quantity = catering.Quantity
                    });
                }
            }

            try
            {
                var createdBooking = await _bookingService.CreateBookingAsync(booking);

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
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
                return Unauthorized();

            // Administrators are not allowed to modify bookings via this endpoint.
            if (currentUser.Role == UserRole.Administrator)
            {
                return Forbid();
            }

            var existing = await _bookingService.GetBookingByIdAsync(id);

            if (existing == null)
            {
                return NotFound();
            }

            // CentreManager: allowed to edit bookings in their location.
            if (currentUser.Role == UserRole.CentreManager)
            {
                if (existing.Boardroom == null || existing.Boardroom.LocationId != currentUser.LocationId)
                {
                    return Forbid();
                }
            }
            else
            {
                // Only the booking owner may modify their booking.
                if (existing.UserId != currentUser.Id)
                {
                    return Forbid();
                }
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
                    catering);

                if (!updated)
                {
                    return NotFound();
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

        // Cancels an existing booking (soft delete - see IBookingService).
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
                return Unauthorized();

            // Administrators are not allowed to delete bookings via this endpoint.
            if (currentUser.Role == UserRole.Administrator)
            {
                return Forbid();
            }

            var existing = await _bookingService.GetBookingByIdAsync(id);

            if (existing == null)
            {
                return NotFound();
            }

            // CentreManager: allowed to cancel bookings in their location.
            if (currentUser.Role == UserRole.CentreManager)
            {
                if (existing.Boardroom == null || existing.Boardroom.LocationId != currentUser.LocationId)
                {
                    return Forbid();
                }
            }
            else
            {
                // Only the booking owner may cancel their booking.
                if (existing.UserId != currentUser.Id)
                {
                    return Forbid();
                }
            }

            try
            {
                var deleted = await _bookingService.DeleteBookingAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Updates the booking status.
        [HttpPatch("{id}/status")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> UpdateBookingStatus(
            int id,
            BookingStatusDto dto)
        {
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
