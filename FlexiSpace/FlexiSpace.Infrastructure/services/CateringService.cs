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
            // Retrieve all catering records from the database
            return await _context.Caterings.ToListAsync();
        }

        public async Task<Catering?> GetCateringByIdAsync(int id)
        {
            // Find the catering item by its primary key
            return await _context.Caterings.FindAsync(id);
        }

        public async Task<Catering> CreateCateringAsync(Catering catering)
        {
            // Add the new catering item to the database
            _context.Caterings.Add(catering);

            // Save the changes
            await _context.SaveChangesAsync();

            // Return the created catering item
            return catering;
        }

        public async Task<bool> UpdateCateringAsync(int id, Catering catering)
        {
            // Find the existing catering item
            var existingCatering = await _context.Caterings.FindAsync(id);

            // Return false if the catering item doesn't exist
            if (existingCatering == null)
            {
                return false;
            }

            // Update the catering details
            existingCatering.Name = catering.Name;
            existingCatering.Description = catering.Description;
            existingCatering.IsActive = catering.IsActive;

            // Save the changes
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteCateringAsync(int id)
        {
            // Find the catering item
            var catering = await _context.Caterings.FindAsync(id);

            // Return false if it doesn't exist
            if (catering == null)
            {
                return false;
            }

            // Remove the catering item
            _context.Caterings.Remove(catering);

            // Save the changes
            await _context.SaveChangesAsync();

            return true;
        }
    }
}