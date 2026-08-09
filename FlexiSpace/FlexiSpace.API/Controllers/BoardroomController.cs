using FlexiSpace.Core.DTOs.Boardroom;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BoardroomController : ControllerBase
    {
        // Service responsible for handling all boardroom-related business operations.
        private readonly IBoardroomService _boardroomService;

        public BoardroomController(IBoardroomService boardroomService)
        {
            _boardroomService = boardroomService;
        }

        // Retrieves all boardrooms together with their assigned equipment.
        [HttpGet]
        public async Task<IActionResult> GetAllBoardrooms()
        {
            var boardrooms = await _boardroomService.GetAllBoardroomsAsync();

            // Convert the entities into DTOs before sending them to the client.
            var response = boardrooms.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single boardroom using its unique ID.
        [HttpGet("{id}")]
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
            // Create an entity containing the updated boardroom information.
            var boardroom = new Boardroom
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Status = dto.Status,
                LocationId = dto.LocationId
            };

            // Convert the incoming equipment DTOs into BoardroomEquipment entities.
            var equipment = dto.Equipment.Select(item => new BoardroomEquipment
            {
                Boardroom = boardroom,
                EquipmentId = item.EquipmentId,
                Quantity = item.Quantity
            }).ToList();

            // Pass both the updated boardroom details and equipment to the service layer.
            var updated = await _boardroomService.UpdateBoardroomAsync(id, boardroom, equipment);

            // Return HTTP 404 if the boardroom could not be found.
            if (!updated)
            {
                return NotFound();
            }

            // HTTP 204 indicates the update completed successfully.
            return NoContent();
        }

        // Deletes a boardroom from the system.
        [HttpDelete("{id}")]
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