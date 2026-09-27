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

        // Sets which boardrooms combine to form this one (replaces the
        // existing list - pass an empty list to clear the combination).
        // Returns false if the combined boardroom does not exist.
        // Throws BusinessRuleException if a boardroom is listed as its own
        // component, if any component boardroom ID does not exist, if a
        // component already belongs to a different combination, or if a
        // component is itself a combined boardroom (nested combinations
        // aren't supported).
        Task<bool> SetBoardroomComponentsAsync(int combinedBoardroomId, List<int> componentBoardroomIds);
    }
}