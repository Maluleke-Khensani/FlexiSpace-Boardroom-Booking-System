using FlexiSpace.API.Authorization;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.DTOs.Location;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlexiSpace.API.Controllers
{
    // Any authenticated user can read locations (branches). Creating,
    // editing or deleting a branch is Administrator-only.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LocationController : AuditableControllerBase
    {
        private readonly ILocationService _locationService;

        public LocationController(
            ILocationService locationService,
            IAuditService auditService,
            ICurrentUserService currentUserService)
            : base(auditService, currentUserService)
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

        // Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPost]
        public async Task<IActionResult> CreateLocation([FromBody] LocationCreateDto locationDto)
        {
            var location = new Location
            {
                Name = locationDto.Name,
                Address = locationDto.Address
            };

            // CreateLocationAsync can throw BusinessRuleException
            // (duplicate name) - return it as 400 instead of an
            // unhandled 500.
            try
            {
                var createdLocation = await _locationService.CreateLocationAsync(location);

                await LogActionAsync(
                    AuditAction.Create,
                    nameof(Location),
                    createdLocation.Id.ToString(),
                    newValues: new { createdLocation.Name, createdLocation.Address });

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

        // Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationUpdateDto locationDto)
        {
            var before = await _locationService.GetLocationByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            var location = new Location
            {
                Name = locationDto.Name,
                Address = locationDto.Address
            };

            // Same as CreateLocation - UpdateLocationAsync can throw
            // BusinessRuleException too.
            try
            {
                var updated = await _locationService.UpdateLocationAsync(id, location);

                if (!updated)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Update,
                    nameof(Location),
                    id.ToString(),
                    oldValues: new { before.Name, before.Address },
                    newValues: new { locationDto.Name, locationDto.Address });

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }

        // Administrator-only.
        [AuthorizeRoles(UserRole.Administrator)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            var before = await _locationService.GetLocationByIdAsync(id);

            if (before == null)
            {
                return NotFound();
            }

            // DeleteLocationAsync can throw BusinessRuleException
            // (boardrooms still assigned to this location).
            try
            {
                var deleted = await _locationService.DeleteLocationAsync(id);

                if (!deleted)
                {
                    return NotFound();
                }

                await LogActionAsync(
                    AuditAction.Delete,
                    nameof(Location),
                    id.ToString(),
                    oldValues: new { before.Name, before.Address });

                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { errors = ex.Errors });
            }
        }
    }
}
