using FlexiSpace.API.Authorization;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.DTOs.Equipment;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlexiSpace.Core.Common;

namespace FlexiSpace.API.Controllers
{
    // Any authenticated user can read the equipment catalogue (they need
    // it to see what a boardroom offers / to request items on a booking).
    // Managing the catalogue itself is Administrator-only.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EquipmentController : AuditableControllerBase
    {
        private readonly IEquipmentService _equipmentService;

        public EquipmentController(
            IEquipmentService equipmentService,
            IAuditService auditService,
            ICurrentUserService currentUserService)
            : base(auditService, currentUserService)
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

        // Creates a new equipment record. Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPost]
        public async Task<IActionResult> CreateEquipment(EquipmentCreateDto dto)
        {
            var equipment = new Equipment
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var createdEquipment = await _equipmentService.CreateEquipmentAsync(equipment);

            await LogActionAsync(
                AuditAction.Create,
                nameof(Equipment),
                createdEquipment.Id.ToString(),
                newValues: new { createdEquipment.Name, createdEquipment.Description });

            return CreatedAtAction(
                nameof(GetEquipmentById),
                new { id = createdEquipment.Id },
                MapToResponseDto(createdEquipment));
        }

        // Updates an existing equipment record. Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEquipment(int id, EquipmentUpdateDto dto)
        {
            var before = await _equipmentService.GetEquipmentByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

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

            await LogActionAsync(
                AuditAction.Update,
                nameof(Equipment),
                id.ToString(),
                oldValues: new { before.Name, before.Description },
                newValues: new { dto.Name, dto.Description });

            return NoContent();
        }

        // Deletes equipment from the system. Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEquipment(int id)
        {
            var before = await _equipmentService.GetEquipmentByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var deleted = await _equipmentService.DeleteEquipmentAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            await LogActionAsync(
                AuditAction.Delete,
                nameof(Equipment),
                id.ToString(),
                oldValues: new { before.Name, before.Description });

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
