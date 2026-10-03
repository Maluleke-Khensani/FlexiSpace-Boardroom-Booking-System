using FlexiSpace.Core.Entities;
using FlexiSpace.Core.DTOs.Equipment;

namespace FlexiSpace.Core.Services
{
    public interface IBoardroomService
    {
        Task<IEnumerable<Boardroom>> GetAllBoardroomsAsync();

        Task<Boardroom?> GetBoardroomByIdAsync(int id);

        Task<Boardroom> CreateBoardroomAsync(Boardroom boardroom);

        Task<bool> UpdateBoardroomAsync(int id, Boardroom boardroom, List<BoardroomEquipment> equipment);

        Task<bool> DeleteBoardroomAsync(int id);
    }
}