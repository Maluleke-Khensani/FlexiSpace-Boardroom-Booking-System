using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class BoardroomService : IBoardroomService
    {
        private readonly ApplicationDbContext _context;

        public BoardroomService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Boardroom>> GetAllBoardroomsAsync()
        {
            return await _context.Boardrooms
                .Include(b => b.Location)
                .Include(b => b.BoardroomEquipments)
                    .ThenInclude(be => be.Equipment)
                .ToListAsync();
        }

        public async Task<Boardroom?> GetBoardroomByIdAsync(int id)
        {
            return await _context.Boardrooms
                .Include(b => b.Location)
                .Include(b => b.BoardroomEquipments)
                    .ThenInclude(be => be.Equipment)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<Boardroom> CreateBoardroomAsync(Boardroom boardroom)
        {
            _context.Boardrooms.Add(boardroom);

            await _context.SaveChangesAsync();

            return boardroom;
        }
        public async Task<bool> UpdateBoardroomAsync(int id,Boardroom updatedBoardroom,List<BoardroomEquipment> equipment)
        {
            var existingBoardroom = await _context.Boardrooms
                .Include(b => b.BoardroomEquipments)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (existingBoardroom == null)
            {
                return false;
            }

            // Update boardroom details
            existingBoardroom.Name = updatedBoardroom.Name;
            existingBoardroom.Capacity = updatedBoardroom.Capacity;
            existingBoardroom.Status = updatedBoardroom.Status;
            existingBoardroom.LocationId = updatedBoardroom.LocationId;

            // Remove existing equipment
            _context.BoardroomEquipments.RemoveRange(existingBoardroom.BoardroomEquipments);

            // Add the new equipment
            existingBoardroom.BoardroomEquipments = equipment;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteBoardroomAsync(int id)
        {
            var boardroom = await _context.Boardrooms.FindAsync(id);

            if (boardroom == null)
            {
                return false;
            }

            _context.Boardrooms.Remove(boardroom);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}