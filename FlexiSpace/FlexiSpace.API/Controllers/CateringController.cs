using FlexiSpace.API.Authorization;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Catering;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Any authenticated user can read the catering catalogue (they need it
    // to request items on a booking). Managing the catalogue itself is
    // Administrator-only.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CateringController : AuditableControllerBase
    {
        private readonly ICateringService _cateringService;

        public CateringController(
            ICateringService cateringService,
            IAuditService auditService,
            ICurrentUserService currentUserService)
            : base(auditService, currentUserService)
        {
            _cateringService = cateringService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCatering()
        {
            var cateringItems = await _cateringService.GetAllCateringAsync();

            var response = cateringItems.Select(catering => new CateringResponseDto
            {
                Id = catering.Id,
                Name = catering.Name,
                Description = catering.Description,
                IsActive = catering.IsActive
            });

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCateringById(int id)
        {
            var catering = await _cateringService.GetCateringByIdAsync(id);

            if (catering == null)
            {
                return NotFound();
            }

            var response = new CateringResponseDto
            {
                Id = catering.Id,
                Name = catering.Name,
                Description = catering.Description,
                IsActive = catering.IsActive
            };

            return Ok(response);
        }

      
        [AuthorizeRoles(UserRole.Administrator, UserRole.CentreManager)]
        [HttpPost]
        public async Task<IActionResult> CreateCatering(CateringCreateDto dto)
        {
            var catering = new Catering
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var createdCatering = await _cateringService.CreateCateringAsync(catering);

            await LogActionAsync(
                AuditAction.Create,
                nameof(Catering),
                createdCatering.Id.ToString(),
                newValues: new { createdCatering.Name, createdCatering.Description });

            var response = new CateringResponseDto
            {
                Id = createdCatering.Id,
                Name = createdCatering.Name,
                Description = createdCatering.Description,
                IsActive = createdCatering.IsActive
            };

            return CreatedAtAction(nameof(GetCateringById),
                new { id = response.Id },
                response);
        }

        // Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCatering(int id, CateringUpdateDto dto)
        {
            var before = await _cateringService.GetCateringByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var catering = new Catering
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive
            };

            var updated = await _cateringService.UpdateCateringAsync(id, catering);

            if (!updated)
            {
                return NotFound();
            }

            await LogActionAsync(
                AuditAction.Update,
                nameof(Catering),
                id.ToString(),
                oldValues: new { before.Name, before.Description, before.IsActive },
                newValues: new { dto.Name, dto.Description, dto.IsActive });

            return NoContent();
        }

        // Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCatering(int id)
        {
            var before = await _cateringService.GetCateringByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            // DeleteCateringAsync can throw BusinessRuleException (catering
            // item still requested on a booking).
            try
            {
                var deleted = await _cateringService.DeleteCateringAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Delete,
                    nameof(Catering),
                    id.ToString(),
                    oldValues: new { before.Name, before.Description });

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }
    }
}
