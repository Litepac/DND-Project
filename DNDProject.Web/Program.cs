using System.Net.Http;
using DNDProject.Web;
using DNDProject.Web.Auth;
using DNDProject.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Client-side auth
builder.Services.AddAuthorizationCore();

// Tokenlager + delegating handler
builder.Services.AddScoped<ITokenStorage, TokenStorage>();
builder.Services.AddScoped<TokenAuthorizationMessageHandler>();

// 🔑 Auth state provider (registrer som konkret type + som base type)
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

// Læs API-baseadresse fra config
var apiBaseAddress =
    builder.Configuration["Api:BaseAddress"] ??
    Environment.GetEnvironmentVariable("Api__BaseAddress") ??
    "http://localhost:5230/";

if (!apiBaseAddress.EndsWith("/"))
    apiBaseAddress += "/";

// Navngiven HttpClient til API'et
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
})
.AddHttpMessageHandler<TokenAuthorizationMessageHandler>();

// Standard HttpClient -> brug den navngivne "Api"
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

// Services
builder.Services.AddScoped<StenaService>();

await builder.Build().RunAsync();
