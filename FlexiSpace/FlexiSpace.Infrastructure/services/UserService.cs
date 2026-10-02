using FlexiSpace.Core.DTOs.User;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEntraUserService _entraUserService;

        public UserService(
            ApplicationDbContext context,
            IEntraUserService entraUserService)
        {
            _context = context;
            _entraUserService = entraUserService;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<bool> UpdateUserAsync(int id, User user)
        {
            var existingUser = await _context.Users.FindAsync(id);

            if (existingUser == null)
            {
                return false;
            }

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Role = user.Role;
            existingUser.LocationId = user.LocationId;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UpdateUserStatusAsync(
            int id,
            bool isActive)
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

        // Provisions an existing Microsoft Entra user into FlexiSpace.
        public async Task<User?> ProvisionUserAsync(
    UserProvisionDto dto)
        {
            var entraUser = await _entraUserService
                .GetUserByIdAsync(dto.EntraObjectId);

            if (entraUser == null)
            {
                return null;
            }

            if (!entraUser.IsActive)
            {
                return null;
            }

            if (!entraUser.Role.HasValue)
            {
                return null;
            }

            // Prevent duplicate local FlexiSpace users (same Entra object).
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.EntraObjectId == dto.EntraObjectId);

            if (existingUser != null)
            {
                return null;
            }

            /*
            // NEW: duplicate-email guard. Two different Entra accounts
            // shouldn't be able to provision into FlexiSpace under the
            // same email - Email is what notifications/calendar invites
            // key off, so it needs to stay unique on its own, separately
            // from the EntraObjectId check above.
            var emailTaken = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == entraUser.Email.Trim().ToLower());

            if (emailTaken)
            {
                return null;
            }
            */
            // Location is optional. If supplied, it must exist.
            if (dto.LocationId.HasValue)
            {
                var locationExists = await _context.Locations
                    .AnyAsync(l =>
                        l.Id == dto.LocationId.Value);

                if (!locationExists)
                {
                    return null;
                }
            }

            var user = new User
            {
                EntraObjectId = entraUser.EntraObjectId,
                FirstName = entraUser.FirstName,
                LastName = entraUser.LastName,
                Email = entraUser.Email,
                Role = entraUser.Role.Value,
                LocationId = dto.LocationId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return user;
        }
    }
}