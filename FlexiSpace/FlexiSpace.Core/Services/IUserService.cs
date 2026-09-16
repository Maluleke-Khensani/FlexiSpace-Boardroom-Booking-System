using FlexiSpace.Core.Entities;

public interface IUserService
{
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByEntraObjectIdAsync(Guid entraObjectId);
    Task<User?> CreateUserAsync(User user);
    Task<bool> UpdateUserAsync(int id, User user);
    Task<bool> UpdateUserStatusAsync(int id, bool isActive);
}