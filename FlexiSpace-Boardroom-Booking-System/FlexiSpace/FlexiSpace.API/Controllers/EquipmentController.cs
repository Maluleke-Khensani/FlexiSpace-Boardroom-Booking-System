using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EquipmentController : ControllerBase
    {
        private readonly IEquipmentService _equipmentService;

        public EquipmentController(IEquipmentService equipmentService)
        {
            _equipmentService = equipmentService;
        }

        // Retrieves all equipment available in the system.
        [HttpGet]
        public async Task<IActionResult> GetAllEquipment()
        {
            var equipment = await _equipmentService
                .GetAllEquipmentAsync();

            var response = equipment.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single piece of equipment using its unique ID.
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetEquipmentById(int id)
        {
            if (id <= 0)
            {
                return BadRequest("Equipment ID must be greater than 0.");
            }

            var equipment = await _equipmentService
                .GetEquipmentByIdAsync(id);

            if (equipment == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(equipment));
        }

        // Creates a new equipment record.
        [HttpPost]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> CreateEquipment(
            EquipmentCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Equipment name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Description))
            {
                return BadRequest("Equipment description is required.");
            }

            var equipment = new Equipment
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var createdEquipment = await _equipmentService
                .CreateEquipmentAsync(equipment);

            return CreatedAtAction(
                nameof(GetEquipmentById),
                new { id = createdEquipment.Id },
                MapToResponseDto(createdEquipment));
        }

        // Updates an existing equipment record.
        [HttpPut("{id:int}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> UpdateEquipment(
            int id,
            EquipmentUpdateDto dto)
        {
            if (id <= 0)
            {
                return BadRequest("Equipment ID must be greater than 0.");
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Equipment name is required.");
            }

            if (string.IsNullOrWhiteSpace(dto.Description))
            {
                return BadRequest("Equipment description is required.");
            }

            var equipment = new Equipment
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive
            };

            var updated = await _equipmentService
                .UpdateEquipmentAsync(id, equipment);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Deletes equipment from the system.
        [HttpDelete("{id:int}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> DeleteEquipment(int id)
        {
            if (id <= 0)
            {
                return BadRequest(
                    "Equipment ID must be greater than 0.");
            }

            // Check that the equipment exists first so that
            // we can distinguish NotFound from a deletion
            // blocked by booking history.
            var equipment = await _equipmentService
                .GetEquipmentByIdAsync(id);

            if (equipment == null)
            {
                return NotFound();
            }

            var deleted = await _equipmentService
                .DeleteEquipmentAsync(id);

            if (!deleted)
            {
                return Conflict(
                    "This equipment has been used in an existing booking " +
                    "and cannot be deleted. Deactivate it instead.");
            }

            return NoContent();
        }

        // Converts an Equipment entity into a response DTO.
        private static EquipmentResponseDto MapToResponseDto(
            Equipment equipment)
        {
            return new EquipmentResponseDto
            {
                Id = equipment.Id,
                Name = equipment.Name,
                Description = equipment.Description,
                IsActive = equipment.IsActive
            };
        }
    }
}