using Microsoft.Extensions.Hosting;
using SFA.DAS.Tools.AnalyseErrorQueues.Functions.Extensions;

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, configBuilder) =>
    {
        configBuilder.AddDasConfiguration(context.HostingEnvironment);
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationServices(context.Configuration);
    })
    .Build();

await host.RunAsync();