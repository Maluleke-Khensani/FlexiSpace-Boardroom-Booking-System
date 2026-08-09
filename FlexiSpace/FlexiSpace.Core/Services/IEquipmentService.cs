using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Services
{
    public interface IEquipmentService
    {
        Task<IEnumerable<Equipment>> GetAllEquipmentAsync();

        Task<Equipment?> GetEquipmentByIdAsync(int id);

        Task<Equipment> CreateEquipmentAsync(Equipment equipment);

        Task<bool> UpdateEquipmentAsync(int id, Equipment equipment);

        Task<bool> UpdateEquipmentStatusAsync(int id, bool isActive);
        Task<bool> DeleteEquipmentAsync(int id);
    }
}