using FlexiSpace.Core.Common;
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

        [HttpGet]
        public async Task<IActionResult> GetAllEquipment()
        {
            var equipment = await _equipmentService.GetAllEquipmentAsync();

            var response = equipment.Select(MapToResponseDto);

            return Ok(response);
        }

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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEquipment(int id, EquipmentUpdateDto dto)
        {
            var equipment = new Equipment
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var updated = await _equipmentService.UpdateEquipmentAsync(id, equipment);

            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEquipment(int id)
        {
            // NEW: DeleteEquipmentAsync can now throw BusinessRuleException
            // (equipment still assigned to a boardroom or booking).
            try
            {
                var deleted = await _equipmentService.DeleteEquipmentAsync(id);

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