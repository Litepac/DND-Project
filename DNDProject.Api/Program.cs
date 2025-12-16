using System.Security.Claims;
using System.Text;
using DNDProject.Api.Data;
using DNDProject.Api.Models;
using DNDProject.Api.ML;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// DATABASES
// ======================================================

// Stena / domain DB (INGEN Identity-tabeller)
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("StenaConnection")));

// Auth / Identity DB (AspNetUsers, AspNetRoles osv.)
builder.Services.AddDbContext<AuthDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("AuthConnection")));

// ======================================================
// SERVICES
// ======================================================

builder.Services.AddControllers();

// ML / data extraction
builder.Services.AddScoped<DNDProject.Api.ML.MLDataService>();
builder.Services.AddScoped<DNDProject.Api.ML.MLTrainerService>();

// Cache af trænet model i memory (hurtigere recommend)
builder.Services.AddSingleton<DNDProject.Api.ML.MLModelStore>();



// ======================================================
// CORS (åben i dev)
// ======================================================

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ======================================================
// SWAGGER + JWT
// ======================================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DNDProject API",
        Version = "v1"
    });

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

// ======================================================
// IDENTITY (AuthDbContext)
// ======================================================

builder.Services
    .AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.Password.RequiredLength = 6;
        opt.Password.RequireDigit = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddSignInManager();

// ======================================================
// JWT AUTHENTICATION
// ======================================================

var jwt = builder.Configuration.GetSection("Jwt");
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        if (builder.Environment.IsDevelopment())
            IdentityModelEventSource.ShowPII = true;

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),

            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = key,

            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

// ======================================================
// AUTHORIZATION
// ALT kræver login (undtagen [AllowAnonymous])
// ======================================================

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ======================================================
// BUILD APP
// ======================================================

var app = builder.Build();

// ======================================================
// PIPELINE
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    IdentityModelEventSource.ShowPII = true;
}

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ======================================================
// AUTH DB MIGRATION + SEED (DEV ONLY)
// RØRER IKKE STENA DB
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var env = services.GetRequiredService<IWebHostEnvironment>();

    var authDb = services.GetRequiredService<AuthDbContext>();
    await authDb.Database.MigrateAsync();

    if (env.IsDevelopment())
    {
        await IdentitySeed.SeedAuthAsync(app.Services);
    }
}

app.Run();
