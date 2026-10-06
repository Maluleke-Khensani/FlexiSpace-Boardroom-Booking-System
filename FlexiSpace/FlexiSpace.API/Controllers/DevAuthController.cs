using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FlexiSpace.API.Services;
using FlexiSpace.Core.Entities;
using FlexiSpace.Core.Enums;
using FlexiSpace.Infrastructure.Persistence;
using FlexiSpace.Infrastructure.services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FlexiSpace.API.Controllers;

/// <summary>
/// Development auth for mobile demo tiles + self-registration + Entra password check.
/// Issues a JWT whose oid matches the user's EntraObjectId so RBAC works unchanged.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DevAuthController(
    ApplicationDbContext context,
    IConfiguration config,
    IHostEnvironment env,
    EntraPasswordLoginService entraPassword) : ControllerBase
{
    public const string SchemeName = "DevAuth";
    public const string Issuer = "FlexiSpace.DevAuth";
    public const string Audience = "FlexiSpace.Mobile";
    private const string DemoPassword = "demo123";

    public record TokenRequest(string Email, string? Password);
    public record RegisterRequest(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string Password);
    public record TokenResponse(string AccessToken, DateTime ExpiresAt, object User);

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!env.IsDevelopment())
            return NotFound();

        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var email = NormalizeEmail(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? string.Empty
            : request.PhoneNumber.Trim();
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return BadRequest(new { message = "First name and last name are required." });

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return BadRequest(new { message = "A valid email address is required." });

        if (password.Length < 6)
            return BadRequest(new { message = "Password must be at least 6 characters." });

        if (phone.Length > 20)
            return BadRequest(new { message = "Phone number is too long." });

        var exists = await context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
        if (exists)
            return Conflict(new { message = "An account with this email already exists. Sign in instead." });

        var user = new User
        {
            EntraObjectId = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phone,
            PasswordHash = DevPasswordHasher.Hash(password),
            Role = UserRole.Client,
            LocationId = null,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return Ok(IssueTokenForUser(user));
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> IssueToken([FromBody] TokenRequest request, CancellationToken ct)
    {
        if (!env.IsDevelopment())
            return NotFound();

        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });

        var user = await FindUserByEmailAsync(email, ct);

        // Local registered / demo password first.
        if (user is not null && PasswordAccepted(user, request.Password))
            return Ok(IssueTokenForUser(user));

        // Real Entra work/guest accounts: validate password with Azure (never stored here).
        if (!string.IsNullOrEmpty(request.Password) && entraPassword.IsConfigured)
        {
            // Prefer the stored directory UPN when the user typed a home email (gmail).
            var entraUserName = user?.Email ?? email;
            var attempt = await entraPassword.TryValidateAsync(entraUserName, request.Password, ct);
            if (attempt.Success is not null)
            {
                user = await EnsureEntraLinkedUserAsync(user, attempt.Success, ct);
                if (user is null)
                {
                    return Unauthorized(new
                    {
                        message = "Microsoft password is correct, but this account is not provisioned in FlexiSpace yet. Ask an admin to add the user."
                    });
                }

                return Ok(IssueTokenForUser(user));
            }

            // Directory row with no local password → steer to interactive Microsoft sign-in
            // (ROPC often fails for guests / MFA even when the password is right).
            if (user is not null && string.IsNullOrEmpty(user.PasswordHash))
            {
                return Unauthorized(new
                {
                    message = "This Microsoft account cannot sign in with email/password here. Tap Sign in with Microsoft instead."
                });
            }
        }

        if (user is null)
            return Unauthorized(new { message = "Unknown account. Register first, or sign in with a Microsoft work account." });

        return Unauthorized(new { message = "Invalid email or password." });
    }

    private static string NormalizeEmail(string? email) => EmailLookup.Normalize(email);

    private async Task<User?> FindUserByEmailAsync(string email, CancellationToken ct)
    {
        var keys = EmailLookup.Candidates(email);
        if (keys.Count == 0) return null;

        var users = await context.Users.Where(u => u.IsActive).ToListAsync(ct);
        return users.FirstOrDefault(u =>
        {
            var stored = EmailLookup.Normalize(u.Email);
            if (keys.Any(k => string.Equals(stored, k, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Typed home email → match guest UPN that embeds local_domain
            return keys.Any(k =>
                !k.Contains('@') &&
                stored.StartsWith(k + "#ext#@", StringComparison.OrdinalIgnoreCase));
        });
    }

    private async Task<User?> EnsureEntraLinkedUserAsync(
        User? existing,
        EntraPasswordLoginService.Result entra,
        CancellationToken ct)
    {
        var user = existing
                   ?? await FindUserByEmailAsync(entra.Email, ct)
                   ?? await context.Users.FirstOrDefaultAsync(
                       u => u.IsActive && u.EntraObjectId == entra.ObjectId, ct);

        if (user is null)
            return null;

        if (user.EntraObjectId != entra.ObjectId)
        {
            user.EntraObjectId = entra.ObjectId;
            await context.SaveChangesAsync(ct);
        }

        return user;
    }

    private TokenResponse IssueTokenForUser(User user)
    {
        var key = GetSigningKey(config);
        var expires = DateTime.UtcNow.AddHours(12);

        var claims = new List<Claim>
        {
            new("oid", user.EntraObjectId.ToString()),
            new(ClaimTypes.NameIdentifier, user.EntraObjectId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("preferred_username", user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new("roles", user.Role.ToString())
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return new TokenResponse(
            jwt,
            expires,
            new
            {
                user.Id,
                Name = $"{user.FirstName} {user.LastName}".Trim(),
                user.Email,
                Role = user.Role.ToString(),
                user.LocationId
            });
    }

    private static bool PasswordAccepted(User user, string? password)
    {
        // Self-registered accounts must match their stored hash.
        if (!string.IsNullOrEmpty(user.PasswordHash))
            return DevPasswordHasher.Verify(password ?? string.Empty, user.PasswordHash);

        // Seeded demo accounts keep the shared demo password.
        return string.Equals(password, DemoPassword, StringComparison.Ordinal)
               || string.IsNullOrEmpty(password); // tolerate older clients that omit password
    }

    public static byte[] GetSigningKey(IConfiguration config)
    {
        var material = config["DevAuth:SigningKey"];
        if (string.IsNullOrWhiteSpace(material))
            material = "FlexiSpace-DevAuth-Local-Signing-Key-Change-Me!";
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }
}
