using System.Security.Claims;
using CdnFileService.Application.Interfaces;
using CdnFileService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CdnFileService.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public async Task<AuthResult> ValidateCredentialsAsync(string userName, string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.AppUsers
            .Include(u => u.Claims)
            .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive, cancellationToken);

        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return AuthResult.Fail;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName)
        };
        claims.AddRange(user.Claims.Select(c => new Claim(c.ClaimType, c.ClaimValue)));

        return new AuthResult(true, user.DisplayName, claims);
    }
}
