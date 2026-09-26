using FlexiSpace.API.Authorization;
using FlexiSpace.API.Controllers.Base;
using FlexiSpace.Core.DTOs.Location;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlexiSpace.Core.Common;

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
            // Retrieving all locations from the service and storing them in a variable
            var locations = await _locationService.GetAllLocationsAsync();

            //Mapping the locations to LocationResponseDto
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
            // Convert the DTO received from the client into a Location entity
            var location = new Location
            {
                Name = locationDto.Name,
                Address = locationDto.Address
            };

            // Send the entity to the service to be saved
            var createdLocation = await _locationService.CreateLocationAsync(location);

            await LogActionAsync(
                AuditAction.Create,
                nameof(Location),
                createdLocation.Id.ToString(),
                newValues: new { createdLocation.Name, createdLocation.Address });

            // Convert the saved entity back into a Response DTO
            var response = new LocationResponseDto
            {
                Id = createdLocation.Id,
                Name = createdLocation.Name,
                Address = createdLocation.Address
            };

            // Return HTTP 201 (Created)
            return CreatedAtAction(
                nameof(GetLocationById),
                new { id = response.Id },
                response);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetLocationById(int id)
        {
            // Ask the service for the requested location
            var location = await _locationService.GetLocationByIdAsync(id);

            // If no location exists, return HTTP 404
            if (location == null)
            {
                return NotFound();
            }

            // Convert the entity into a Response DTO
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

            // Convert the Update DTO into a Location entity
            var location = new Location
            {
                Name = locationDto.Name,
                Address = locationDto.Address
            };

            // Ask the service to update the location
            var updated = await _locationService.UpdateLocationAsync(id, location);

            // If the location wasn't found, return 404
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

            // Return HTTP 204 (No Content)
            return NoContent();
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

            // Ask the service to delete the location
            var deleted = await _locationService.DeleteLocationAsync(id);

            // Return 404 if the location doesn't exist
            if (!deleted)
            {
                return NotFound();
            }

            await LogActionAsync(
                AuditAction.Delete,
                nameof(Location),
                id.ToString(),
                oldValues: new { before.Name, before.Address });

            // Return HTTP 204 (No Content)
            return NoContent();
        }
    }
}
