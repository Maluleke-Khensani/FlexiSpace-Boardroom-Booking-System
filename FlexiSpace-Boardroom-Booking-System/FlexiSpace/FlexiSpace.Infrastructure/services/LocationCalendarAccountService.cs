using FlexiSpace.Core.DTOs.Location;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.services
{
    public class LocationCalendarAccountService : ILocationCalendarAccountService
    {
        private readonly ApplicationDbContext _context;

        public LocationCalendarAccountService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<LocationCalendarAccount> CreateAsync(LocationCalendarAccountCreateDto dto)
        {
            // Ensure location exists
            var location = await _context.Locations.FindAsync(dto.LocationId);

            if (location == null)
            {
                throw new KeyNotFoundException($"Location {dto.LocationId} not found.");
            }

            var account = new LocationCalendarAccount
            {
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                IsPrimary = dto.IsPrimary,
                IsActive = dto.IsActive,
                LocationId = dto.LocationId,
                Location = location
            };

            _context.LocationCalendarAccounts.Add(account);

            await _context.SaveChangesAsync();

            return account;
        }
    }
}
