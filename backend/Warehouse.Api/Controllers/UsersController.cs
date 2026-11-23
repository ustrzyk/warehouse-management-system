
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.DTOs.Common;
using Warehouse.Application.DTOs.Users;
using Warehouse.Infrastructure.Identity;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = WarehouseRoles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly WarehouseDbContext _db;
    private readonly IConfiguration _configuration;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        WarehouseDbContext db,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(
                "Page must be >= 1 and pageSize between 1 and 100.");
        }

        long offset = (long)(page - 1) * pageSize;

        if (offset > int.MaxValue)
            return BadRequest("Page offset is too large.");

        var query = _userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .ThenBy(x => x.Id);

        var totalCount = await query.CountAsync();

        var users = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<UserDto>();

        foreach (var user in users)
            items.Add(await ToDtoAsync(user));

        return Ok(new PagedResult<UserDto>(
            items,
            totalCount,
            page,
            pageSize));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        return Ok(await ToDtoAsync(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(
        [FromBody] CreateUserRequest request)
    {
        var role = request.Role.Trim();

        if (!IsSupportedRole(role))
            return BadRequest("Invalid warehouse role.");

        if (!await _roleManager.RoleExistsAsync(role))
            return StatusCode(500, "Required role is missing.");

        var email = request.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
            return Conflict("User with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FullName = request.FullName.Trim(),
            IsActive = true,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(
            user, request.Password);

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
            user, role);

        if (!roleResult.Succeeded)
        {
            var deleteResult = await _userManager.DeleteAsync(user);

            return StatusCode(500, new
            {
                Message = deleteResult.Succeeded
                    ? "Role assignment failed. Account rolled back."
                    : "Role assignment failed. Manual cleanup required."
            });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = user.Id },
            await ToDtoAsync(user));
    }

    [HttpPut("{id}/role")]
    public async Task<ActionResult<UserDto>> ChangeRole(
        string id,
        [FromBody] ChangeUserRoleRequest request)
    {
        var role = request.Role.Trim();

        if (!IsSupportedRole(role))
            return BadRequest("Invalid warehouse role.");

        if (!await _roleManager.RoleExistsAsync(role))
            return StatusCode(500, "Required role is missing.");

        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Count == 1 &&
            currentRoles[0] == role)
        {
            return Ok(await ToDtoAsync(user));
        }

        // Do not allow an administrator to change their own role.
        if (IsCurrentUser(user))
        {
            return Conflict(
                "You cannot change your own administrator role.");
        }

        var isAdministrator = currentRoles.Contains(
            WarehouseRoles.Administrator);

        if (isAdministrator &&
            role != WarehouseRoles.Administrator)
        {
            if (IsBootstrapAdministrator(user))
            {
                return Conflict(
                    "Bootstrap administrator role is protected.");
            }

            if (user.IsActive && await IsLastActiveAdminAsync())
            {
                return Conflict(
                    "Cannot remove the last active administrator.");
            }
        }

        // All Identity changes use the same SQL transaction.
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        // Revoke previously issued JWT tokens before role changes.
        var stampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        if (!stampResult.Succeeded)
            return StatusCode(500, "Token revocation failed.");

        if (currentRoles.Count > 0)
        {
            var removeResult =
                await _userManager.RemoveFromRolesAsync(
                    user, currentRoles);

            if (!removeResult.Succeeded)
                return StatusCode(500, "Removing old role failed.");
        }

        var addResult = await _userManager.AddToRoleAsync(
            user, role);

        if (!addResult.Succeeded)
            return StatusCode(500, "Assigning new role failed.");

        await transaction.CommitAsync();

        return Ok(await ToDtoAsync(user));
    }

    [HttpPut("{id}/active")]
    public async Task<ActionResult<UserDto>> SetActive(
        string id,
        [FromBody] SetUserActiveRequest request)
    {
        if (request.IsActive is null)
            return BadRequest("isActive is required.");

        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
            return NotFound();

        var newStatus = request.IsActive.Value;

        if (user.IsActive == newStatus)
            return Ok(await ToDtoAsync(user));

        if (!newStatus)
        {
            if (IsCurrentUser(user))
            {
                return Conflict(
                    "You cannot deactivate your own account.");
            }

            if (IsBootstrapAdministrator(user))
            {
                return Conflict(
                    "Bootstrap administrator cannot be deactivated.");
            }

            if (await _userManager.IsInRoleAsync(
                user, WarehouseRoles.Administrator))
            {
                if (await IsLastActiveAdminAsync())
                {
                    return Conflict(
                        "Cannot deactivate the last active administrator.");
                }
            }
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        // Revoke tokens on both deactivation and reactivation.
        var stampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        if (!stampResult.Succeeded)
            return StatusCode(500, "Token revocation failed.");

        user.IsActive = newStatus;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
            return StatusCode(500, "Updating account failed.");

        await transaction.CommitAsync();

        return Ok(await ToDtoAsync(user));
    }

    private async Task<UserDto> ToDtoAsync(
        ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.IsActive,
            roles.ToArray());
    }

    private bool IsCurrentUser(ApplicationUser user)
    {
        var currentUserId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return user.Id == currentUserId;
    }

    private bool IsBootstrapAdministrator(
        ApplicationUser user)
    {
        var bootstrapEmail =
            _configuration["BootstrapAdmin:Email"]?.Trim();

        return !string.IsNullOrWhiteSpace(bootstrapEmail) &&
            string.Equals(
                user.Email,
                bootstrapEmail,
                StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsLastActiveAdminAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync(
            WarehouseRoles.Administrator);

        return admins.Count(x => x.IsActive) <= 1;
    }

    private static bool IsSupportedRole(string role)
    {
        return role == WarehouseRoles.Administrator ||
               role == WarehouseRoles.WarehouseWorker ||
               role == WarehouseRoles.Manager;
    }
}
