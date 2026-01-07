# Blogpost 2 – Web Service Design & Implementation

## Formål

Denne blogpost beskriver, hvordan vi har designet og implementeret vores **RESTful Web API**, giver et samlet overblik over API’ets endpoints, samt forklarer hvordan vi anvender **file storage (browserens localStorage)** til håndtering af JWT-baseret autentifikation i frontend.

---

## 1) Arbejde med RESTful Web API

Backend er implementeret som et **RESTful API i ASP.NET Core**.  
API’et fungerer som det centrale lag mellem databaserne og frontend-applikationen (Blazor WebAssembly).

Vores tilgang har været:

- Ét controller-ansvar pr. domæneområde
- REST-konventioner (GET til data, POST til handlinger)
- Server-side beregning og aggregering
- DTO’er som kontrakt mellem backend og frontend
- JWT-baseret autentifikation

---

## API-konfiguration (Program.cs)

### Databaser

Vi anvender to databaser:

- **AppDbContext** til domænedata (Stena)
- **AuthDbContext** til brugere og roller (Identity)

```csharp
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("StenaConnection")));

builder.Services.AddDbContext<AuthDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("AuthConnection")));
```

## Identity & JWT Authentication

```csharp
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = key
        };
    });
```

## Alle endpoints kræver login som standard

```csharp
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

### Overblik over Web API Endpoints

API’et er dokumenteret via Swagger og opdelt i følgende hovedområder.

## Auth

- POST /api/auth/login
- GET /api/auth/ping-protected

## ML

- POST /api/ml/train
- POST /api/ml/recommend
- GET /api/ml/recommend-one
- GET /api/ml/baseline
- GET /api/ml/baseline-v2

## Stena Customer Dashboard

GET /api/stena/customers/summary
GET /api/stena/customers/{customerKey}/dashboard

### AuthController – Login og beskyttede endpoints

## Login-endpoint

```csharp
[HttpPost("login")]
[AllowAnonymous]
public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
{
    var user = await _userManager.FindByEmailAsync(req.Email);
    if (user is null) return Unauthorized();

    var ok = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
    if (!ok.Succeeded) return Unauthorized();

    var (token, expires, roles, custId) = await CreateJwtAsync(user);
    return new LoginResponse(token, expires, user.Email!, roles, custId);
}
Beskyttet test-endpoint
[HttpGet("ping-protected")]
[Authorize]
public IActionResult PingProtected()
{
    return Ok(new { message = "JWT token er gyldigt" });
}
```

### MLController – Maskinlæring og anbefalinger

## MLController håndterer

- Træning af model
- Anbefaling af container-setup
- Baseline- og effektberegninger
- Træning af model

```csharp
[HttpPost("train")]
public async Task<IActionResult> Train(DateTime? from, DateTime? to)
{
    var result = await _trainer.TrainAsync(from!.Value, to!.Value);
    return Ok(result);
}
```

## Anbefaling for én kunde

```csharp
[HttpGet("recommend-one")]
public async Task<IActionResult> RecommendOne(string customerNo)
{
    var baseRec = await _trainer.RecommendOneAsync(
        DateTime.Now.AddYears(-1),
        DateTime.Now,
        customerNo);

    var eng = _engine.Recommend(
        baseRec.PredKgPerDaySafe,
        0.13,
        baseRec.FrequencyDays,
        0.95,
        0.80,
        1.05,
        30
    );

    return Ok(new
    {
        customerNo,
        containerL = eng.ContainerSize,
        containerCount = eng.ContainerCount
    });
}
```

### File storage – localStorage til JWT

Frontend (Blazor WebAssembly) anvender browserens localStorage til at gemme JWT-token.

## TokenStorage

```csharp
public sealed class TokenStorage : ITokenStorage
{
    private readonly IJSRuntime _js;
    private const string Key = "auth_token";

    public TokenStorage(IJSRuntime js) => _js = js;

    public Task SaveAsync(string token) =>
        _js.InvokeVoidAsync("localStorage.setItem", Key, token).AsTask();

    public Task<string?> GetAsync() =>
        _js.InvokeAsync<string?>("localStorage.getItem", Key).AsTask();

    public Task ClearAsync() =>
        _js.InvokeVoidAsync("localStorage.removeItem", Key).AsTask();
}
```

## Automatisk vedhæftning af Bearer-token

```csharp
public sealed class TokenAuthorizationMessageHandler : DelegatingHandler
{
    private readonly ITokenStorage _tokens;

    public TokenAuthorizationMessageHandler(ITokenStorage tokens) =>
        _tokens = tokens;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```
