using FlexiSpace.API.Authorization;
using FlexiSpace.Core.DTOs.Catering;
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
    public class CateringController : ControllerBase
    {
        private readonly ICateringService _cateringService;

        public CateringController(ICateringService cateringService)
        {
            _cateringService = cateringService;
        }

        // GET: api/Catering
        // Authenticated users can view available catering options.
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

        // GET: api/Catering/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCateringById(int id)
        {
            if (id <= 0)
            {
                return BadRequest("Catering ID must be greater than 0.");
            }

            var catering = await _cateringService.GetCateringByIdAsync(id);

            if (catering == null)
            {
                return NotFound($"Catering item with ID {id} was not found.");
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

        // POST: api/Catering
        // Only administrators can create catering options.
        [HttpPost]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> CreateCatering(
            [FromBody] CateringCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Catering name is required.");
            }

            var catering = new Catering
            {
                Name = dto.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description.Trim(),
                IsActive = true
            };

            var createdCatering =
                await _cateringService.CreateCateringAsync(catering);

            var response = new CateringResponseDto
            {
                Id = createdCatering.Id,
                Name = createdCatering.Name,
                Description = createdCatering.Description,
                IsActive = createdCatering.IsActive
            };

            return CreatedAtAction(
                nameof(GetCateringById),
                new { id = response.Id },
                response);
        }

        // PUT: api/Catering/{id}
        // Only administrators can update catering options.
        [HttpPut("{id:int}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> UpdateCatering(
            int id,
            [FromBody] CateringUpdateDto dto)
        {
            if (id <= 0)
            {
                return BadRequest("Catering ID must be greater than 0.");
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("Catering name is required.");
            }

            var catering = new Catering
            {
                Name = dto.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? null
                    : dto.Description.Trim(),
                IsActive = dto.IsActive
            };

            var updated =
                await _cateringService.UpdateCateringAsync(id, catering);

            if (!updated)
            {
                return NotFound($"Catering item with ID {id} was not found.");
            }

            return NoContent();
        }

        // DELETE: api/Catering/{id}
        // Only administrators can delete catering options.
        [HttpDelete("{id:int}")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> DeleteCatering(int id)
        {
            if (id <= 0)
            {
                return BadRequest("Catering ID must be greater than 0.");
            }

            var catering = await _cateringService.GetCateringByIdAsync(id);

            if (catering == null)
            {
                return NotFound($"Catering item with ID {id} was not found.");
            }

            var deleted = await _cateringService.DeleteCateringAsync(id);

            if (!deleted)
            {
                return Conflict(
                    "This catering item cannot be deleted because it is linked to an existing booking. " +
                    "Set IsActive to false instead.");
            }

            return NoContent();
        }
    }
}