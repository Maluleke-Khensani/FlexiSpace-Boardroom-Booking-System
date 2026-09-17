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

        // The database context is used to access and modify
        // FlexiSpace users and locations in the database.
        //
        // IEntraUserService is used to retrieve the selected user's
        // identity information from Microsoft Entra ID.
        public UserService(
            ApplicationDbContext context,
            IEntraUserService entraUserService)
        {
            _context = context;
            _entraUserService = entraUserService;
        }

        // Retrieves all users from the FlexiSpace database.
        //
        // This returns users who have already been provisioned
        // into the FlexiSpace system.
        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        // Retrieves a single FlexiSpace user by their database ID.
        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await _context.Users.FindAsync(id);
        }

        // Updates an existing FlexiSpace user's information.
        //
        // Only the fields that are allowed to be changed through
        // the user-management functionality are updated here.
        //
        // EntraObjectId and Email are not changed because they come
        // from Microsoft Entra ID and identify the user.
        public async Task<bool> UpdateUserAsync(int id, User user)
        {
            var existingUser = await _context.Users.FindAsync(id);

            // If the user does not exist in the FlexiSpace database,
            // there is nothing to update.
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

        // Activates or deactivates a user account within FlexiSpace.
        //
        // This does not disable the user's Microsoft Entra account.
        // It only controls whether the user is active in the
        // FlexiSpace application.
        public async Task<bool> UpdateUserStatusAsync(
            int id,
            bool isActive)
        {
            var existingUser = await _context.Users.FindAsync(id);

            // If the user does not exist, the status cannot be changed.
            if (existingUser == null)
            {
                return false;
            }

            existingUser.IsActive = isActive;

            await _context.SaveChangesAsync();

            return true;
        }

        // Provisions an existing Microsoft Entra user into FlexiSpace.
        //
        // Admin-first provisioning means that the administrator
        // selects an existing Entra user and then assigns their
        // FlexiSpace role and optional location.
        //
        // The user's name and email are NOT supplied by the admin.
        // They are retrieved directly from Microsoft Entra ID.
        public async Task<User?> ProvisionUserAsync(
    UserProvisionDto dto)
        {
            // Retrieve the selected user from Microsoft Entra ID.
            var entraUser = await _entraUserService
                .GetUserByIdAsync(dto.EntraObjectId);

            if (entraUser == null)
            {
                return null;
            }

            // Only active Entra accounts can be provisioned.
            if (!entraUser.IsActive)
            {
                return null;
            }

            // The user must have a valid FlexiSpace application role
            // assigned in Microsoft Entra ID.
            //
            // The Administrator does NOT choose the role during
            // provisioning.
            if (!entraUser.Role.HasValue)
            {
                return null;
            }

            // Prevent duplicate local FlexiSpace users.
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.EntraObjectId == dto.EntraObjectId);

            if (existingUser != null)
            {
                return null;
            }

            // Location is optional.
            // If supplied, it must exist in the FlexiSpace database.
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

            // Create the local FlexiSpace user.
            //
            // Identity information and role come from Entra ID.
            // Location comes from the FlexiSpace Administrator.
            var user = new User
            {
                EntraObjectId = entraUser.EntraObjectId,

                FirstName = entraUser.FirstName,

                LastName = entraUser.LastName,

                Email = entraUser.Email,

                // Role comes directly from the Entra app role.
                Role = entraUser.Role.Value,

                // Location is selected by the FlexiSpace Administrator.
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