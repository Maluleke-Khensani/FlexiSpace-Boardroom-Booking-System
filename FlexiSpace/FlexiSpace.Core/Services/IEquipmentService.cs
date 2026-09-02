using FlexiSpace.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexiSpace.Core.Services
{
    public interface IEquipmentService
    {
        Task<IEnumerable<Equipment>> GetAllEquipmentAsync();

        Task<Equipment?> GetEquipmentByIdAsync(int id);

        Task<Equipment> CreateEquipmentAsync(Equipment equipment);

        Task<bool> UpdateEquipmentAsync(int id, Equipment equipment);

        Task<bool> DeleteEquipmentAsync(int id);
    }
}