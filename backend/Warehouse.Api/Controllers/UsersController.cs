
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Warehouse.Application.DTOs.Users;
using Warehouse.Infrastructure.Identity;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = WarehouseRoles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.IsActive,
            roles.ToArray()));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(
        [FromBody] CreateUserRequest request)
    {
        var role = request.Role.Trim();

        // Only supported warehouse roles can be assigned.
        if (role != WarehouseRoles.Administrator &&
            role != WarehouseRoles.WarehouseWorker &&
            role != WarehouseRoles.Manager)
        {
            return BadRequest(new
            {
                Message = "Invalid warehouse role."
            });
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            return StatusCode(500, new
            {
                Message = "Required role is not configured."
            });
        }

        var email = request.Email.Trim();

        var existingUser =
            await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Conflict(new
            {
                Message = "User with this email already exists."
            });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            IsActive = true,
            LockoutEnabled = true,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                Errors = createResult.Errors
                    .Select(x => x.Description)
                    .ToArray()
            });
        }

        var roleResult = await _userManager.AddToRoleAsync(
            user,
            role);

        if (!roleResult.Succeeded)
        {
            // Try to remove an incomplete account.
            var deleteResult =
                await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                Message = deleteResult.Succeeded
                    ? "User role assignment failed. Account rolled back."
                    : "User role assignment failed. Manual cleanup required."
            });
        }

        var response = new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.IsActive,
            new[] { role });

        return CreatedAtAction(
            nameof(GetById),
            new { id = user.Id },
            response);
    }
}
