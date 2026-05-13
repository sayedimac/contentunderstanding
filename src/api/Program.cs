using Azure.Core;
using Azure.Identity;
using ContentUnderstanding.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient<ContentUnderstandingService>();
        services.AddSingleton<TokenCredential, DefaultAzureCredential>();
    })
    .Build();

host.Run();
