using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using web;
using web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["ApiBaseAddress"];

if (string.IsNullOrWhiteSpace(apiBaseAddress))
{
    apiBaseAddress = builder.HostEnvironment.BaseAddress.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        ? "http://localhost:7071/"
        : builder.HostEnvironment.BaseAddress;
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress, UriKind.Absolute) });
builder.Services.AddScoped<ContentUnderstandingClient>();

await builder.Build().RunAsync();
