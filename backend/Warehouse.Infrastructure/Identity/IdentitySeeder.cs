
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Warehouse.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        // Create missing roles
        string[] roles =
        {
            WarehouseRoles.Administrator,
            WarehouseRoles.WarehouseWorker,
            WarehouseRoles.Manager
        };

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(
                new IdentityRole(roleName));

            EnsureSuccess(
                result,
                $"Tworzenie roli {roleName}");
        }

        // Read administrator credentials
        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Brak konfiguracji BootstrapAdmin w User Secrets.");
        }

        // Check existing administrator
        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            if (!await userManager.IsInRoleAsync(
                existingUser,
                WarehouseRoles.Administrator))
            {
                throw new InvalidOperationException(
                    "Konto BootstrapAdmin istnieje, " +
                    "ale nie ma roli Administrator.");
            }

            // Enable account lockout for existing admin
            if (!await userManager.GetLockoutEnabledAsync(
                existingUser))
            {
                var lockoutResult =
                    await userManager.SetLockoutEnabledAsync(
                        existingUser,
                        true);

                EnsureSuccess(
                    lockoutResult,
                    "Wlaczenie blokowania administratora");
            }

            return;
        }

        // Create initial administrator
        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Development Administrator",
            IsActive = true,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(
            admin,
            password);

        EnsureSuccess(
            createResult,
            "Tworzenie administratora");

        // Assign Administrator role
        var roleResult = await userManager.AddToRoleAsync(
            admin,
            WarehouseRoles.Administrator);

        EnsureSuccess(
            roleResult,
            "Przypisywanie roli Administrator");
    }

    private static void EnsureSuccess(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join(
            "; ",
            result.Errors.Select(x => x.Description));

        throw new InvalidOperationException(
            $"{operation}: {errors}");
    }
}
