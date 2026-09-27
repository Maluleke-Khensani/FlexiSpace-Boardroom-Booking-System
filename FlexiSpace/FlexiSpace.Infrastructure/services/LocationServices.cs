using System;
using FlexiSpace.Core.Common;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class LocationService : ILocationService
    {

        private readonly ApplicationDbContext _context;

        public LocationService(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<IEnumerable<Location>> GetAllLocationsAsync()
        {
            return await _context.Locations.ToListAsync();
        }

        public async Task<Location?> GetLocationByIdAsync(int id)
        {
            return await _context.Locations.FindAsync(id);
        }

        public async Task<Location> CreateLocationAsync(Location location)
        {
            // NEW: duplicate-name guard. Trimmed and compared case-
            // insensitively so "Centurion" and " centurion " count as the
            // same name.
            var nameTaken = await _context.Locations
                .AnyAsync(l => l.Name.ToLower() == location.Name.Trim().ToLower());

            if (nameTaken)
            {
                throw new BusinessRuleException(
                    $"A location named '{location.Name}' already exists.");
            }

            _context.Locations.Add(location);

            await _context.SaveChangesAsync();

            return location;
        }

        public async Task<bool> UpdateLocationAsync(int id, Location updatedLocation)
        {
            var existingLocation = await _context.Locations.FindAsync(id);

            if (existingLocation == null)
            {
                return false;
            }

            // NEW: same duplicate-name guard, excluding this location
            // itself so saving it unchanged doesn't trip over its own name.
            var nameTaken = await _context.Locations
                .AnyAsync(l => l.Id != id
                    && l.Name.ToLower() == updatedLocation.Name.Trim().ToLower());

            if (nameTaken)
            {
                throw new BusinessRuleException(
                    $"A location named '{updatedLocation.Name}' already exists.");
            }

            existingLocation.Name = updatedLocation.Name;
            existingLocation.Address = updatedLocation.Address;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteLocationAsync(int id)
        {
            var location = await _context.Locations.FindAsync(id);

            if (location == null)
            {
                return false;
            }

            // NEW: delete guard. A location with boardrooms still assigned
            // to it can't be removed - that would orphan every boardroom,
            // booking, and user tied to it.
            var hasBoardrooms = await _context.Boardrooms
                .AnyAsync(b => b.LocationId == id);

            if (hasBoardrooms)
            {
                throw new BusinessRuleException(
                    "This location can't be deleted because it still has boardrooms assigned to it. " +
                    "Reassign or delete those boardrooms first.");
            }

            _context.Locations.Remove(location);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}