using System;
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

            _context.Locations.Remove(location);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}
