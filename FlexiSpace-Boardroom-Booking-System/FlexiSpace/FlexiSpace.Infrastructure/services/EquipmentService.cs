using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class EquipmentService : IEquipmentService
    {
        private readonly ApplicationDbContext _context;

        public EquipmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Equipment>> GetAllEquipmentAsync()
        {
            return await _context.Equipments
                .OrderBy(e => e.Name)
                .ToListAsync();
        }

        public async Task<Equipment?> GetEquipmentByIdAsync(int id)
        {
            return await _context.Equipments
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Equipment> CreateEquipmentAsync(Equipment equipment)
        {
            equipment.Name = equipment.Name.Trim();
            equipment.Description = equipment.Description.Trim();

            // New equipment is available by default.
            equipment.IsActive = true;
            equipment.CreatedAt = DateTime.UtcNow;

            _context.Equipments.Add(equipment);

            await _context.SaveChangesAsync();

            return equipment;
        }

        public async Task<bool> UpdateEquipmentAsync(
            int id,
            Equipment equipment)
        {
            var existingEquipment = await _context.Equipments
                .FirstOrDefaultAsync(e => e.Id == id);

            if (existingEquipment == null)
            {
                return false;
            }

            existingEquipment.Name = equipment.Name.Trim();

            existingEquipment.Description =
                equipment.Description.Trim();

            existingEquipment.IsActive = equipment.IsActive;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteEquipmentAsync(int id)
        {
            var equipment = await _context.Equipments
                .FirstOrDefaultAsync(e => e.Id == id);

            if (equipment == null)
            {
                return false;
            }

            // Equipment that has been requested in a booking
            // must not be permanently deleted because it forms
            // part of booking history.
            var isUsedInBooking = await _context.BookingEquipments
                .AnyAsync(be => be.EquipmentId == id);

            if (isUsedInBooking)
            {
                return false;
            }

            _context.Equipments.Remove(equipment);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}