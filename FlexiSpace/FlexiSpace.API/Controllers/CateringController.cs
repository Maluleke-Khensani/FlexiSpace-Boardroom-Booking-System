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
            // Retrieve all catering items from the service
            var cateringItems = await _cateringService.GetAllCateringAsync();

            // Convert the entities into Response DTOs
            var response = cateringItems.Select(catering => new CateringResponseDto
            {
                Id = catering.Id,
                Name = catering.Name,
                Description = catering.Description,
                IsActive = catering.IsActive
            });

            // Return HTTP 200 (OK)
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCateringById(int id)
        {
            // Ask the service for the requested catering item
            var catering = await _cateringService.GetCateringByIdAsync(id);

            // Return HTTP 404 if the catering item doesn't exist
            if (catering == null)
            {
                return NotFound();
            }

            // Convert the entity into a Response DTO
            var response = new CateringResponseDto
            {
                Id = catering.Id,
                Name = catering.Name,
                Description = catering.Description,
                IsActive = catering.IsActive
            };

            // Return HTTP 200 (OK)
            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCatering(CateringCreateDto dto)
        {
            // Convert the Create DTO into a Catering entity
            var catering = new Catering
            {
                Name = dto.Name,
                Description = dto.Description
            };

            // Ask the service to create the catering item
            var createdCatering = await _cateringService.CreateCateringAsync(catering);

            // Convert the created entity into a Response DTO
            var response = new CateringResponseDto
            {
                Id = createdCatering.Id,
                Name = createdCatering.Name,
                Description = createdCatering.Description,
                IsActive = createdCatering.IsActive
            };

            // Return HTTP 201 (Created)
            return CreatedAtAction(nameof(GetCateringById),
                new { id = response.Id },
                response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCatering(int id, CateringUpdateDto dto)
        {
            // Convert the Update DTO into a Catering entity
            var catering = new Catering
            {
                Name = dto.Name,
                Description = dto.Description,
                IsActive = dto.IsActive
            };

            // Ask the service to update the catering item
            var updated = await _cateringService.UpdateCateringAsync(id, catering);

            // Return HTTP 404 if the catering item doesn't exist
            if (!updated)
            {
                return NotFound();
            }

            // Return HTTP 204 (No Content)
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCatering(int id)
        {
            // Ask the service to delete the catering item
            var deleted = await _cateringService.DeleteCateringAsync(id);

            // Return HTTP 404 if the catering item doesn't exist
            if (!deleted)
            {
                return NotFound();
            }

            // Return HTTP 204 (No Content)
            return NoContent();
        }
    }
}