using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Services;
using FlexiSpace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlexiSpace.Infrastructure.Services
{
    public class DeviceTokenService : IDeviceTokenService
    {
        private readonly ApplicationDbContext _context;

        public DeviceTokenService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RegisterTokenAsync(int userId, string token, string platform)
        {
            var existing = await _context.DeviceTokens
                .FirstOrDefaultAsync(d => d.Token == token);

            if (existing != null)
            {
                // Same device, possibly a different user (shared device,
                // or the previous user signed out) - re-point it rather
                // than creating a duplicate row (Token is unique).
                existing.UserId = userId;
                existing.Platform = platform;
                existing.LastSeenAt = DateTime.UtcNow;
            }
            else
            {
                _context.DeviceTokens.Add(new DeviceToken
                {
                    UserId = userId,
                    Token = token,
                    Platform = platform
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> RemoveTokenAsync(string token)
        {
            var existing = await _context.DeviceTokens
                .FirstOrDefaultAsync(d => d.Token == token);

            if (existing == null)
            {
                return false;
            }

            _context.DeviceTokens.Remove(existing);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IReadOnlyList<string>> GetTokensForUserAsync(int userId)
        {
            return await _context.DeviceTokens
                .Where(d => d.UserId == userId)
                .Select(d => d.Token)
                .ToListAsync();
        }
    }
}
