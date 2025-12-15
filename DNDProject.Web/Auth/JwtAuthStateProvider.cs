using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DNDProject.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;


namespace DNDProject.Web.Auth;

public sealed class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokens;

    public JwtAuthStateProvider(ITokenStorage tokens)
    {
        _tokens = tokens;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokens.GetAsync();

        if (string.IsNullOrWhiteSpace(token))
            return Anonymous();

        var principal = TryBuildPrincipal(token);
        return new AuthenticationState(principal);
    }

    public async Task MarkUserAsAuthenticatedAsync(string jwt)
    {
        await _tokens.SaveAsync(jwt);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(TryBuildPrincipal(jwt))));
    }

    public async Task MarkUserAsLoggedOutAsync()
    {
        await _tokens.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
    }

    private static AuthenticationState Anonymous()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static ClaimsPrincipal TryBuildPrincipal(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            // Roles kommer ind som ClaimTypes.Role hvis du udsteder dem sådan (som i din AuthController)
            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
