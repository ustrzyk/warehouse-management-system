
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Warehouse.Application.DTOs.Authentication;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Api.Authentication;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public async Task<LoginResponse> CreateAsync(
        ApplicationUser user)
    {
        // Pobieram konfigurację JWT z ustawień.
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Missing Jwt:Key.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Missing Jwt:Issuer.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Missing Jwt:Audience.");

        // Pobieram role i aktualny security stamp użytkownika.
        var roles = await _userManager.GetRolesAsync(user);

        var securityStamp =
            await _userManager.GetSecurityStampAsync(user);

        // Składam claims, które trafią do tokenu.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("security_stamp", securityStamp)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Ustawiam czas życia tokenu i klucz podpisu.
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(30);

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var serializedToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(
            serializedToken,
            "Bearer",
            expires,
            user.FullName,
            roles.ToArray());
    }
}
