using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Retrieves all users from the database.
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        // Retrieves a single user by their ID.
        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        // Updates an existing user's information.
        public async Task<bool> UpdateUserAsync(int id, User user)
        {
            var existingUser = await _context.Users.FindAsync(id);

            if (existingUser == null)
            {
                return false;
            }

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.Role = user.Role;
            existingUser.LocationId = user.LocationId;

            await _context.SaveChangesAsync();

            return true;
        }

        // Activates or deactivates a user account.
        public async Task<bool> UpdateUserStatusAsync(int id, bool isActive)
        {
            var existingUser = await _context.Users.FindAsync(id);

            if (existingUser == null)
            {
                return false;
            }

            existingUser.IsActive = isActive;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}