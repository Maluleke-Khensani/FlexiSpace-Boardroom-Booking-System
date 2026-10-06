using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.DTOs.Boardroom;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BoardroomController : ControllerBase
    {
        // Service responsible for handling all boardroom-related business operations.
        private readonly IBoardroomService _boardroomService;
        private readonly FlexiSpace.Core.Common.ICurrentUserService _currentUserService;

        public BoardroomController(IBoardroomService boardroomService, FlexiSpace.Core.Common.ICurrentUserService currentUserService)
        {
            _boardroomService = boardroomService;
            _currentUserService = currentUserService;
        }

        // Retrieves all boardrooms together with their assigned equipment.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllBoardrooms()
        {
            var boardrooms = await _boardroomService.GetAllBoardroomsAsync();

            // Convert the entities into DTOs before sending them to the client.
            var response = boardrooms.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single boardroom using its unique ID.
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBoardroomById(int id)
        {
            var boardroom = await _boardroomService.GetBoardroomByIdAsync(id);

            // Return HTTP 404 if no matching boardroom exists.
            if (boardroom == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(boardroom));
        }

        // Creates a new boardroom and assigns the selected equipment.
        [HttpPost]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> CreateBoardroom(BoardroomCreateDto dto)
        {
            // Convert the incoming DTO into a Boardroom entity.
            var boardroom = new Boardroom
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Status = dto.Status,
                LocationId = dto.LocationId
            };

            // Build the list of equipment assigned to this boardroom.
            foreach (var item in dto.Equipment)
            {
                boardroom.BoardroomEquipments.Add(new BoardroomEquipment
                {
                    Boardroom = boardroom,
                    EquipmentId = item.EquipmentId,
                    Quantity = item.Quantity
                });
            }

            // Save the new boardroom and its equipment.
            var createdBoardroom = await _boardroomService.CreateBoardroomAsync(boardroom);

            // Return HTTP 201 with the newly created resource.
            return CreatedAtAction(
                nameof(GetBoardroomById),
                new { id = createdBoardroom.Id },
                MapToResponseDto(createdBoardroom));
        }

        // Updates an existing boardroom and replaces its equipment list.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBoardroom(int id, BoardroomUpdateDto dto)
        {
            var existing = await _boardroomService.GetBoardroomByIdAsync(id);

            if (existing == null)
            {
                return NotFound();
            }

            var currentUser = await _currentUserService.GetCurrentUserAsync();

            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Administrators can perform full updates.
            if (currentUser.Role == UserRole.Administrator)
            {
                var boardroom = new Boardroom
                {
                    Name = dto.Name,
                    Capacity = dto.Capacity,
                    Status = dto.Status,
                    LocationId = dto.LocationId
                };

                var equipment = dto.Equipment.Select(item => new BoardroomEquipment
                {
                    Boardroom = boardroom,
                    EquipmentId = item.EquipmentId,
                    Quantity = item.Quantity
                }).ToList();

                var updated = await _boardroomService.UpdateBoardroomAsync(id, boardroom, equipment);

                if (!updated)
                {
                    return NotFound();
                }

                return NoContent();
            }

            // CentreManagers may only change the room Status for boardrooms in their location.
            if (currentUser.Role == UserRole.CentreManager)
            {
                if (existing.LocationId != currentUser.LocationId)
                {
                    return Forbid();
                }

                // Do not allow CentreManagers to change name/capacity/location/equipment.
                // Only allow status changes (e.g., block/unblock).
                if (dto.Name != existing.Name || dto.Capacity != existing.Capacity || dto.LocationId != existing.LocationId || (dto.Equipment?.Count ?? 0) != existing.BoardroomEquipments.Count)
                {
                    return Forbid();
                }

                var updatedBoardroom = new Boardroom
                {
                    Name = existing.Name,
                    Capacity = existing.Capacity,
                    Status = dto.Status,
                    LocationId = existing.LocationId
                };

                var updated = await _boardroomService.UpdateBoardroomAsync(id, updatedBoardroom, existing.BoardroomEquipments.ToList());

                if (!updated)
                    return NotFound();

                return NoContent();
            }

            return Forbid();
        }

        // Deletes a boardroom from the system.
        [HttpDelete("{id}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> DeleteBoardroom(int id)
        {
            var deleted = await _boardroomService.DeleteBoardroomAsync(id);

            // Return HTTP 404 if the boardroom doesn't exist.
            if (!deleted)
            {
                return NotFound();
            }

            // HTTP 204 indicates the resource was successfully deleted.
            return NoContent();
        }

        // Converts a Boardroom entity into a response DTO.
        // Keeping the mapping in one place avoids repeating the same code in multiple endpoints.
        private static BoardroomResponseDto MapToResponseDto(Boardroom boardroom)
        {
            return new BoardroomResponseDto
            {
                Id = boardroom.Id,
                Name = boardroom.Name,
                Capacity = boardroom.Capacity,
                Status = boardroom.Status,
                LocationId = boardroom.LocationId,

                Equipment = boardroom.BoardroomEquipments
                    .Select(be => new BoardroomEquipmentDto
                    {
                        EquipmentId = be.EquipmentId,
                        Quantity = be.Quantity
                    })
                    .ToList()
            };
        }
    }
}