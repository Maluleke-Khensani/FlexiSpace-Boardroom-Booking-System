using FlexiSpace.Core.Common;
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
            return await _context.Equipments.ToListAsync();
        }

        public async Task<Equipment?> GetEquipmentByIdAsync(int id)
        {
            return await _context.Equipments.FindAsync(id);
        }

        public async Task<Equipment> CreateEquipmentAsync(Equipment equipment)
        {
            _context.Equipments.Add(equipment);

            await _context.SaveChangesAsync();

            return equipment;
        }

        public async Task<bool> UpdateEquipmentAsync(int id, Equipment equipment)
        {
            var existingEquipment = await _context.Equipments.FindAsync(id);

            if (existingEquipment == null)
            {
                return false;
            }

            existingEquipment.Name = equipment.Name;
            existingEquipment.Description = equipment.Description;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteEquipmentAsync(int id)
        {
            var equipment = await _context.Equipments.FindAsync(id);

            if (equipment == null)
            {
                return false;
            }

            // NEW: delete guard. Equipment still attached to a boardroom's
            // inventory, or referenced by a booking's requested extras,
            // can't be removed - that would strip it out from under
            // records that still expect it.
            var inUse = await _context.BoardroomEquipments.AnyAsync(be => be.EquipmentId == id)
                || await _context.BookingEquipments.AnyAsync(be => be.EquipmentId == id);

            if (inUse)
            {
                throw new BusinessRuleException(
                    "This equipment can't be deleted because it's still assigned to a boardroom or booking.");
            }

            _context.Equipments.Remove(equipment);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}