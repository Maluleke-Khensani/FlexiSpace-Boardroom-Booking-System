using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Catering;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CateringController : ControllerBase
    {
        private readonly ICateringService _cateringService;

        public CateringController(ICateringService cateringService)
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

        [HttpPost]
        public async Task<IActionResult> CreateCatering(CateringCreateDto dto)
        {
            var catering = new Catering
            {
                Name = dto.Name,
                Description = dto.Description
            };

            var createdCatering = await _cateringService.CreateCateringAsync(catering);

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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCatering(int id, CateringUpdateDto dto)
        {
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

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCatering(int id)
        {
            // NEW: DeleteCateringAsync can now throw BusinessRuleException
            // (catering item still requested on a booking).
            try
            {
                var deleted = await _cateringService.DeleteCateringAsync(id);

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
    }
}