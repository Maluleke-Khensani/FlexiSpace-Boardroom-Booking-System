using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Services
{
    public interface ICateringService
    {
        Task<IEnumerable<Catering>> GetAllCateringAsync();

        Task<Catering?> GetCateringByIdAsync(int id);

        Task<Catering> CreateCateringAsync(Catering catering);

        Task<bool> UpdateCateringAsync(int id, Catering catering);

        Task<bool> DeleteCateringAsync(int id);
    }
}