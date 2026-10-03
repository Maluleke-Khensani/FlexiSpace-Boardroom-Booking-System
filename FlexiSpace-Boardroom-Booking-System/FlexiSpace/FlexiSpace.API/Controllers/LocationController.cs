using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FlexiSpace.API.Authorization;
using FlexiSpace.Core.Enums;
using FlexiSpace.Core.DTOs.Location;
using FlexiSpace.Core.Services;
using FlexiSpace.Core.Entities;

namespace FlexiSpace.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [AuthorizeRoles(UserRole.Administrator)]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILocationCalendarAccountService _locationCalendarAccountService;

        public LocationController(ILocationService locationService, ILocationCalendarAccountService locationCalendarAccountService)
        {
            _locationService = locationService;
            _locationCalendarAccountService = locationCalendarAccountService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllLocations()
        {
            // Retrieving all locations from the service and stroing them in a variable
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

        // Creates a calendar account for a location (e.g. centre manager calendar).
        [HttpPost("calendar-accounts")]
        [AuthorizeRoles(UserRole.Administrator)]
        public async Task<IActionResult> CreateLocationCalendarAccount([FromBody] LocationCalendarAccountCreateDto dto)
        {
            try
            {
                var account = await _locationCalendarAccountService.CreateAsync(dto);

                var response = new LocationCalendarAccountResponseDto
                {
                    Id = account.Id,
                    Email = account.Email,
                    DisplayName = account.DisplayName,
                    IsPrimary = account.IsPrimary,
                    IsActive = account.IsActive,
                    LocationId = account.LocationId,
                    CreatedAt = account.CreatedAt
                };

                return CreatedAtAction(nameof(GetLocationById), new { id = account.LocationId }, response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

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
        [AllowAnonymous]
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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateLocation(int id, [FromBody] LocationUpdateDto locationDto)
        {
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

            // Return HTTP 204 (No Content)
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLocation(int id)
        {
            // Ask the service to delete the location
            var deleted = await _locationService.DeleteLocationAsync(id);

            // Return 404 if the location doesn't exist
            if (!deleted)
            {
                return NotFound();
            }

            // Return HTTP 204 (No Content)
            return NoContent();
        }


    }
}

