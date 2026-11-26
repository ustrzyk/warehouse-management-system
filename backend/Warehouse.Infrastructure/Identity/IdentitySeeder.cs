
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

        // Tworzę brakujące role systemowe.
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

        // Czytam dane konta administratora z konfiguracji.
        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Brak konfiguracji BootstrapAdmin w User Secrets.");
        }

        // Sprawdzam, czy administrator już istnieje.
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

            // Włączam blokadę konta dla istniejącego administratora.
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

        // Tworzę początkowe konto administratora.
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

        // Przypisuję rolę Administrator do nowego konta.
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
