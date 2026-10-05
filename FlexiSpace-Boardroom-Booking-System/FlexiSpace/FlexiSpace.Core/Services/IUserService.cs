using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Entities;

namespace FlexiSpace.Core.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsersAsync();

        Task<User?> GetUserByIdAsync(int id);

        Task<bool> UpdateUserAsync(int id, User user);

        Task<bool> UpdateUserStatusAsync(int id, bool isActive);
        Task<User?> ProvisionUserAsync(UserProvisionDto dto);
        Task<User?> CreateDirectoryUserAsync(UserDirectoryCreateDto dto);
    }
}