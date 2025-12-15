using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DNDProject.Api.Data;

public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("AuthConnection")
                 ?? throw new InvalidOperationException("Missing ConnectionStrings:AuthConnection");

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AuthDbContext(options);
    }
}
