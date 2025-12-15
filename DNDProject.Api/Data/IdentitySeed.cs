using DNDProject.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DNDProject.Api.Data;

public static class IdentitySeed
{
    public static async Task SeedAuthAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Roles
        string[] roles = { "Admin", "Sales" };
        foreach (var r in roles)
        {
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));
        }

        // Admin
        await EnsureUserAsync(
            userMgr,
            email: "admin@stena",
            password: "admin123",
            roles: new[] { "Admin" });

        // Sales
        await EnsureUserAsync(
            userMgr,
            email: "sales@stena",
            password: "sales123",
            roles: new[] { "Sales" });

        // Ralleboy (kan alt)
        await EnsureUserAsync(
            userMgr,
            email: "ralleboy@gud",
            password: "gud123",
            roles: new[] { "Admin", "Sales" });
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userMgr,
        string email,
        string password,
        IEnumerable<string> roles)
    {
        var user = await userMgr.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var created = await userMgr.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                var msg = string.Join("; ", created.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Could not create user {email}: {msg}");
            }
        }

        foreach (var role in roles)
        {
            if (!await userMgr.IsInRoleAsync(user, role))
                await userMgr.AddToRoleAsync(user, role);
        }
    }
}
