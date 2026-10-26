using FlexiSpace.API.Authorization;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.Common;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EquipmentController : AuditableControllerBase
    {
        private readonly IEquipmentService _equipmentService;

        public EquipmentController(IEquipmentService equipmentService, IAuditService auditService, ICurrentUserService currentUserService) : base(auditService, currentUserService)
        {
            _equipmentService = equipmentService;
        }

        // Retrieves all equipment available in the system.
        // Guests and clients can browse the catalogue; only administrators can change it.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllEquipment()
        {
            var equipment = await _equipmentService
                .GetAllEquipmentAsync();

            var response = equipment.Select(MapToResponseDto);

            return Ok(response);
        }

        // Retrieves a single piece of equipment using its unique ID.
        [HttpGet("{id:int}")]
        [AllowAnonymous]
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
            await LogActionAsync(AuditAction.Create, "Equipment", createdEquipment.Id.ToString(), null,
                new { createdEquipment.Name, createdEquipment.Description, createdEquipment.IsActive });

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

            await LogActionAsync(AuditAction.Update, "Equipment", id.ToString(), null,
                new { equipment.Name, equipment.Description, equipment.IsActive });

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

            await LogActionAsync(AuditAction.Delete, "Equipment", id.ToString(),
                new { equipment.Name, equipment.Description, equipment.IsActive }, null);

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
