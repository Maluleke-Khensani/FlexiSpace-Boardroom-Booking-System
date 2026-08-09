using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            var equipment = await _equipmentService.GetAllEquipmentAsync();

            var response = equipment.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single piece of equipment using its unique ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEquipmentById(int id)
        {
            var equipment = await _equipmentService.GetEquipmentByIdAsync(id);

            if (equipment == null)
            {
                return NotFound();
            }

            return Ok(MapToResponseDto(equipment));
        }

        // Creates a new equipment record.
        [HttpPost]
        public async Task<IActionResult> CreateEquipment(EquipmentCreateDto dto)
        {
            var equipment = new Equipment
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var createdEquipment = await _equipmentService.CreateEquipmentAsync(equipment);

            return CreatedAtAction(
                nameof(GetEquipmentById),
                new { id = createdEquipment.Id },
                MapToResponseDto(createdEquipment));
        }


        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateEquipmentStatus(int id,EquipmentStatusDto dto)
        {
            // Ask the service to update the equipment status
            var updated = await _equipmentService.UpdateEquipmentStatusAsync(id, dto.IsActive);

            // Return 404 if the equipment wasn't found
            if (!updated)
            {
                return NotFound();
            }

            // Return 204 when the update succeeds
            return NoContent();
        }
        // Updates an existing equipment record.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEquipment(int id, EquipmentUpdateDto dto)
        {
            // Convert the Update DTO into an Equipment entity
            var equipment = new Equipment
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive
            };

            // Ask the service to update the equipment
            var updated = await _equipmentService.UpdateEquipmentAsync(id, equipment);

            // Return 404 if the equipment doesn't exist
            if (!updated)
            {
                return NotFound();
            }

            // Return HTTP 204 (No Content)
            return NoContent();
        }
        // Deletes equipment from the system.
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEquipment(int id)
        {
            var deleted = await _equipmentService.DeleteEquipmentAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }

        // Converts an Equipment entity into a response DTO.
        private static EquipmentResponseDto MapToResponseDto(Equipment equipment)
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

