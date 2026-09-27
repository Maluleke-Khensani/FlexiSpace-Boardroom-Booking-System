using FlexiSpace.Core.Common;
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

            var response = boardrooms.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single boardroom using its unique ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetBoardroomById(int id)
        {
            var boardroom = await _boardroomService.GetBoardroomByIdAsync(id);

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
            var boardroom = new Boardroom
            {
                Name = dto.Name,
                Capacity = dto.Capacity,
                Status = dto.Status,
                LocationId = dto.LocationId
            };

            foreach (var item in dto.Equipment)
            {
                boardroom.BoardroomEquipments.Add(new BoardroomEquipment
                {
                    Boardroom = boardroom,
                    EquipmentId = item.EquipmentId,
                    Quantity = item.Quantity
                });
            }

            // NEW: CreateBoardroomAsync can now throw BusinessRuleException
            // (duplicate name at this location, or an unknown EquipmentId).
            try
            {
                var createdBoardroom = await _boardroomService.CreateBoardroomAsync(boardroom);

                return CreatedAtAction(
                    nameof(GetBoardroomById),
                    new { id = createdBoardroom.Id },
                    MapToResponseDto(createdBoardroom));
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Updates an existing boardroom and replaces its equipment list.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBoardroom(int id, BoardroomUpdateDto dto)
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

            // NEW: same as CreateBoardroom - UpdateBoardroomAsync can now
            // throw BusinessRuleException too.
            try
            {
                var updated = await _boardroomService.UpdateBoardroomAsync(id, boardroom, equipment);

                if (!updated)
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

        // Deletes a boardroom from the system.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBoardroom(int id)
        {
            // NEW: DeleteBoardroomAsync can now throw BusinessRuleException
            // (existing bookings reference this boardroom).
            try
            {
                var deleted = await _boardroomService.DeleteBoardroomAsync(id);

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

        // Sets which boardrooms combine to form this one - e.g. linking
        // "Thingamajik" and "Whachamacallit" as the components of a bigger
        // conjoined room (mirrors the combination feature already shipped
        // in the mobile app). Replaces the existing component list.
        [HttpPut("{id}/components")]
        public async Task<IActionResult> SetBoardroomComponents(int id, BoardroomComponentsDto dto)
        {
            try
            {
                var updated = await _boardroomService.SetBoardroomComponentsAsync(id, dto.ComponentBoardroomIds);

                if (!updated)
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
                    .ToList(),

                ComponentBoardroomIds = boardroom.Components
                    .Select(c => c.ComponentBoardroomId)
                    .ToList(),

                CombinedIntoBoardroomIds = boardroom.PartOfCombinations
                    .Select(c => c.CombinedBoardroomId)
                    .ToList()
            };
        }
    }
}