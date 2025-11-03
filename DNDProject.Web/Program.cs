using System.Net.Http;
using DNDProject.Web;
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

// Læs API-baseadresse fra config
var apiBaseAddress =
    builder.Configuration["Api:BaseAddress"] ??
    Environment.GetEnvironmentVariable("Api__BaseAddress") ??
    "http://localhost:5230";

// Navngiven HttpClient til API'et
builder.Services.AddHttpClient("Api", client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
})
.AddHttpMessageHandler<TokenAuthorizationMessageHandler>();

// Standard HttpClient -> brug den navngivne "Api"
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

// 👉 her registrerer vi StenaService, nu hvor HttpClient findes
builder.Services.AddScoped<StenaService>();

await builder.Build().RunAsync();
