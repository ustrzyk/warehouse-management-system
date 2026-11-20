
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Api.Authentication;
using Warehouse.Application.DTOs.Authentication;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || !user.IsActive)
            return Unauthorized();

        if (await _userManager.IsLockedOutAsync(user))
            return Unauthorized();

        var validPassword = await _userManager.CheckPasswordAsync(
            user, request.Password);

        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized();
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var response = await _jwtTokenService.CreateAsync(user);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null || !user.IsActive)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            Roles = roles
        });
    }
}
