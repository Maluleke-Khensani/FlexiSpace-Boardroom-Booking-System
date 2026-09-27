using FlexiSpace.Core.Common;
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
            return await _context.Caterings.ToListAsync();
        }

        public async Task<Catering?> GetCateringByIdAsync(int id)
        {
            return await _context.Caterings.FindAsync(id);
        }

        public async Task<Catering> CreateCateringAsync(Catering catering)
        {
            _context.Caterings.Add(catering);

            await _context.SaveChangesAsync();

            return catering;
        }

        public async Task<bool> UpdateCateringAsync(int id, Catering catering)
        {
            var existingCatering = await _context.Caterings.FindAsync(id);

            if (existingCatering == null)
            {
                return false;
            }

            existingCatering.Name = catering.Name;
            existingCatering.Description = catering.Description;
            existingCatering.IsActive = catering.IsActive;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteCateringAsync(int id)
        {
            var catering = await _context.Caterings.FindAsync(id);

            if (catering == null)
            {
                return false;
            }

            // NEW: delete guard. A catering option still requested on an
            // existing booking can't be removed - that would strip it off
            // bookings that still list it.
            var inUse = await _context.BookingCaterings.AnyAsync(bc => bc.CateringId == id);

            if (inUse)
            {
                throw new BusinessRuleException(
                    "This catering option can't be deleted because it's still requested on a booking. " +
                    "Set IsActive to false instead if it's no longer offered.");
            }

            _context.Caterings.Remove(catering);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}