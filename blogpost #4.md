# Blogpost 4 – User Management

## Formål

Denne blogpost beskriver, hvordan brugere og roller er håndteret i systemet, hvordan login og autentifikation er implementeret, samt hvordan adgang til ressourcer mellem forskellige aktører (Sales og Admin) er designet. Hvor funktionalitet ikke nåede at blive fuldt implementeret, er dette tydeligt beskrevet.

---

## 1) Brugertyper og roller i systemet

Systemet understøtter to primære roller:

- **Admin**
  - Ansvarlig for systemadministration og brugerhåndtering
- **Sales**
  - Arbejder med kunder, container-effektivitet og ML-baserede anbefalinger

Rollerne er implementeret via **ASP.NET Core Identity** og gemmes i autentifikationsdatabasen. Brugere kan have én eller flere roller.

---

## 2) Oprettelse af brugere og roller (IdentitySeed)

Ved applikationsstart seedes både roller og testbrugere. Dette sikrer et kendt og konsistent setup under udvikling og test.

```csharp
string[] roles = { "Admin", "Sales" };
foreach (var r in roles)
{
    if (!await roleMgr.RoleExistsAsync(r))
        await roleMgr.CreateAsync(new IdentityRole(r));
}
```

## Der oprettes herefter brugere med forskellige roller

```csharp
await EnsureUserAsync(
    userMgr,
    email: "admin@stena",
    password: "admin123",
    roles: new[] { "Admin" });

await EnsureUserAsync(
    userMgr,
    email: "sales@stena",
    password: "sales123",
    roles: new[] { "Sales" });
```

Dette gør det muligt at teste rollebaseret adgang i både API og frontend.

## Login og autentifikation (JWT)

Login håndteres via et REST-endpoint i API’et. Når brugeren logger ind, valideres credentials via Identity, og der udstedes et JWT-token.

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


JWT-tokenet indeholder:

Brugerens identitet

Roller (ClaimTypes.Role)

Eventuel kunde-relation (customerId)
```

## Login i frontend (Blazor WebAssembly)

Frontend har en dedikeret login-side, hvor brugeren logger ind via API’et. Token gemmes og bruges til efterfølgende requests.

```csharp
var resp = await Http.PostAsJsonAsync("api/auth/login", model);
var dto = await resp.Content.ReadFromJsonAsync<LoginResponse>();

await AuthState.MarkUserAsAuthenticatedAsync(dto.Token);
Nav.NavigateTo("/home", replace: true);
```

Token gemmes i browserens localStorage via JavaScript-interop.

## Beskyttelse af sider i frontend (RequireLogin)

For at sikre at kun autentificerede brugere kan tilgå systemet, anvendes en wrapper-komponent:

```csharp
@if (_allowed)
{
    @ChildContent
}
```

Komponenten tjekker om brugeren er logget ind, og viderestiller ellers til /login.

```csharp
var state = await Auth.GetAuthenticationStateAsync();
if (state.User?.Identity?.IsAuthenticated != true)
{
    Nav.NavigateTo("/login", replace: true);
}
```

Dette sikrer, at hele applikationen er beskyttet mod uautoriseret adgang.

## Rolleinformation i UI

Efter login aflæses brugerens roller fra JWT-claims og vises i navigationen:

```csharp
roles = user.Claims
    .Where(c => c.Type == ClaimTypes.Role)
    .Select(c => c.Value)
    .Distinct()
    .ToList();
```

Dette gør det muligt at:

- Vise hvilken rolle brugeren har
- Forberede rollebaseret UI-logik (fx Admin-funktioner)

## Ressourceadgang mellem aktører (status)

Alle API-endpoints er beskyttet med [Authorize], hvilket betyder at kun autentificerede brugere kan tilgå systemets data.

```csharp
[ApiController]
[Route("api/ml")]
[Authorize]
public sealed class MLController : ControllerBase
```

Rollebaseret adgang (ikke færdigimplementeret)

Systemet er designet til rollebaseret adgang (fx [Authorize(Roles = "Admin")]), men denne differentiering nåede ikke at blive fuldt implementeret på endpoint-niveau inden projektets afslutning.

- Roller findes
- Roller udstedes i JWT
- Roller læses i frontend
- Men endpoints skelner endnu ikke mellem Admin og Sales

Dette er en bevidst og dokumenteret begrænsning i den nuværende løsning.

## Sikkerhed og vurdering

Selvom rollebaseret adgang ikke er fuldt udnyttet, lever løsningen op til grundlæggende sikkerhedskrav:

- Kun autentificerede brugere har adgang
- JWT anvendes korrekt
- Roller er integreret i Identity-modellen
- Arkitekturen er klar til udvidelse

Løsningen vurderes derfor som funktionelt korrekt, sikker i praksis, og klar til videreudvikling.
