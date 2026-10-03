using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class CateringService : ICateringService
    {
        private readonly ApplicationDbContext _context;

        public CateringService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Catering>> GetAllCateringAsync()
        {
            return await _context.Caterings
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Catering?> GetCateringByIdAsync(int id)
        {
            return await _context.Caterings
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<Catering> CreateCateringAsync(Catering catering)
        {
            catering.Name = catering.Name.Trim();

            if (catering.Description != null)
            {
                catering.Description = catering.Description.Trim();
            }

            catering.IsActive = true;
            catering.CreatedAt = DateTime.UtcNow;

            _context.Caterings.Add(catering);

            await _context.SaveChangesAsync();

            return catering;
        }

        public async Task<bool> UpdateCateringAsync(int id, Catering catering)
        {
            var existingCatering = await _context.Caterings
                .FirstOrDefaultAsync(c => c.Id == id);

            if (existingCatering == null)
            {
                return false;
            }

            existingCatering.Name = catering.Name.Trim();

            existingCatering.Description =
                catering.Description?.Trim();

            existingCatering.IsActive = catering.IsActive;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteCateringAsync(int id)
        {
            var catering = await _context.Caterings
                .FirstOrDefaultAsync(c => c.Id == id);

            if (catering == null)
            {
                return false;
            }

            // A catering item that has been used in a booking
            // must not be deleted because it is part of booking history.
            var isUsedInBooking = await _context.BookingCaterings
                .AnyAsync(bc => bc.CateringId == id);

            if (isUsedInBooking)
            {
                return false;
            }

            _context.Caterings.Remove(catering);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}