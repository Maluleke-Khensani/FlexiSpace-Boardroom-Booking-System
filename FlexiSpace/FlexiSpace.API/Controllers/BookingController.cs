using FlexiSpace.Core.DTOs.Booking;
using FlexiSpace.Core.DTOs.Catering;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        // Retrieves all bookings.
        [HttpGet]
        public async Task<IActionResult> GetAllBookings()
        {
            var bookings = await _bookingService.GetAllBookingsAsync();

            var response = bookings.Select(MapToResponseDto);

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

            var createdBooking = await _bookingService.CreateBookingAsync(booking);

            return CreatedAtAction(
                nameof(GetBookingById),
                new { id = createdBooking.Id },
                MapToResponseDto(createdBooking));
        }

        // Updates an existing booking.
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

        // Deletes an existing booking.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var deleted = await _bookingService.DeleteBookingAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Updates the booking status.
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(
            int id,
            BookingStatusDto dto)
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