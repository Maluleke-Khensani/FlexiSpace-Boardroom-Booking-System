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

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            // Retrieves all users stored in the FlexiSpace database.
            return await _context.Users.ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            // Finds a FlexiSpace user using their local database ID.
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetUserByEntraObjectIdAsync(Guid entraObjectId)
        {
            // Finds the FlexiSpace user linked to the
            // Entra ID account using the Entra Object ID.
            return await _context.Users
                .FirstOrDefaultAsync(u => u.EntraObjectId == entraObjectId);
        }


        public async Task<User?> CreateUserAsync(User user)
        {
            // Prevents the same Entra account from being registered more than once.
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.EntraObjectId == user.EntraObjectId);

            if (existingUser != null)
                return null;

            // Adds the new FlexiSpace user to the database.
            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<bool> UpdateUserAsync(int id, User user)
        {
            // Finds the existing user before updating their details.
            var existingUser = await _context.Users.FindAsync(id);

            if (existingUser == null)
                return false;

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Role = user.Role;
            existingUser.LocationId = user.LocationId;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateUserStatusAsync(int id, bool isActive)
        {
            // Finds the user whose active status needs to be changed.
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return false;

            user.IsActive = isActive;

            await _context.SaveChangesAsync();

            return true;
        }
    }
}