using Microsoft.AspNetCore.Mvc;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Location;
using FlexiSpace.Core.Services;
using FlexiSpace.Core.Entities;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;

        public LocationController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllLocations()
        {
            var locations = await _locationService.GetAllLocationsAsync();

            var response = locations.Select(location => new LocationResponseDto
            {
                Id = location.Id,
                Name = location.Name,
                Address = location.Address,

            });

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> CreateLocation([FromBody] LocationCreateDto locationDto)
        {
            var location = new Location
            {
                Name = locationDto.Name,
                Address = locationDto.Address
            };

            // NEW: CreateLocationAsync can now throw BusinessRuleException
            // (duplicate name) - return it as 400 instead of an
            // unhandled 500.
            try
            {
                var createdLocation = await _locationService.CreateLocationAsync(location);

                var response = new LocationResponseDto
                {
                    Id = createdLocation.Id,
                    Name = createdLocation.Name,
                    Address = createdLocation.Address
                };

                return CreatedAtAction(
                    nameof(GetLocationById),
                    new { id = response.Id },
                    response);
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetLocationById(int id)
        {
            var location = await _locationService.GetLocationByIdAsync(id);

            if (location == null)
            {
                return NotFound();
            }

            var response = new LocationResponseDto
            {
                Id = location.Id,
                Name = location.Name,
                Address = location.Address
            };

            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationUpdateDto locationDto)
        {
            var location = new Location
            {
                Name = locationDto.Name,
                Address = locationDto.Address
            };

            // NEW: same as CreateLocation - UpdateLocationAsync can now
            // throw BusinessRuleException too.
            try
            {
                var updated = await _locationService.UpdateLocationAsync(id, location);

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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            // NEW: DeleteLocationAsync can now throw BusinessRuleException
            // (boardrooms still assigned to this location).
            try
            {
                var deleted = await _locationService.DeleteLocationAsync(id);

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