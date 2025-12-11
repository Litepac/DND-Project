using DNDProject.Api.Data;
using DNDProject.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SQL Server  👈 ændret fra SQLite
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Controllers
builder.Services.AddControllers();

// CORS (åben i dev)
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Swagger + JWT-knap
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DNDProject API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Indsæt JWT token her (uden 'Bearer ' foran).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// Identity
builder.Services
    .AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.Password.RequiredLength = 6;
        opt.Password.RequireDigit = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

// JWT config (midlertidigt: kun signaturvalidering)
var jwt = builder.Configuration.GetSection("Jwt");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // Vis detaljerede fejl (KUN i udvikling)
        IdentityModelEventSource.ShowPII = true;

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = key
        };

        // Ekstra log – viser præcis hvad der kommer ind,
        // trimmer evt. tegn og logger fejlbeskeden.
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                               .GetRequiredService<ILoggerFactory>()
                               .CreateLogger("JWT");

                var rawAuth = ctx.Request.Headers["Authorization"].ToString();
                logger.LogInformation("Authorization header modtaget: {auth}", rawAuth);

                // Trim "Bearer " + evt. anførselstegn/whitespace
                if (!string.IsNullOrWhiteSpace(rawAuth) &&
                    rawAuth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Token = rawAuth.Substring("Bearer ".Length).Trim().Trim('"');
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                               .GetRequiredService<ILoggerFactory>()
                               .CreateLogger("JWT");

                logger.LogError(ctx.Exception, "JWT authentication failed: {msg}", ctx.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                               .GetRequiredService<ILoggerFactory>()
                               .CreateLogger("JWT");

                logger.LogInformation("JWT token er valideret for {sub}",
                    ctx.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true; // kun i dev
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// --- Seeding & migrations i et scope ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var env = services.GetRequiredService<IWebHostEnvironment>();
    var db  = services.GetRequiredService<AppDbContext>();

    // Vi kører ikke EF-migrationer mod Stena-databasen
    // await db.Database.MigrateAsync();

    // Midlertidigt: slå Identity seeding fra, fordi der ikke findes en Customers-tabel i Stena-DB
    // if (env.IsDevelopment())
    //     await IdentitySeed.SeedAsync(app.Services);

    //----- await StenaDataSeed.SeedAsync(app.Services);
}


app.Run();
