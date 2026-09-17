using FlexiSpace.Core.DTOs.User;

namespace FlexiSpace.Core.Services
{
    public interface IEntraUserService
    {
        Task<IEnumerable<EntraUserResponseDto>> GetUsersAsync();

        Task<EntraUserResponseDto?> GetUserByIdAsync(
            Guid entraObjectId);
    }
}