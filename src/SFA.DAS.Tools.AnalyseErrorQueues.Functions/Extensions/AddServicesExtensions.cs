using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain.Configuration;
using SFA.DAS.Tools.AnalyseErrorQueues.Engine;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.SvcBusService;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ApplicationConfiguration>()
                .Configure(config.Bind);

        services.AddOptions<ServiceBusRepoSettings>()
                .Configure(config.GetSection(nameof(ServiceBusRepoSettings)).Bind);

        services.AddOptions<LADataSinkSettings>()
                .Configure(config.GetSection(nameof(LADataSinkSettings)).Bind);

        services.AddOptions<BlobDataSinkSettings>()
                .Configure(config.GetSection(nameof(BlobDataSinkSettings)).Bind);

        services.AddLogging();

        services.AddTransient<IDataSink, BlobDataSink>();
        services.AddTransient<ISvcBusService, SvcBusService>();

        services.AddTransient<IAnalyseQueues, QueueAnalyser>(sp =>
        {
            var sink = sp.GetRequiredService<IDataSink>();
            var svc = sp.GetRequiredService<ISvcBusService>();
            var log = sp.GetRequiredService<ILogger<QueueAnalyser>>();
            var serviceBusSettings = sp.GetRequiredService<IOptions<ServiceBusRepoSettings>>();
            return new QueueAnalyser(sink, svc, serviceBusSettings, log);
        });

        services.AddTransient<IAnalyseQueuesBase, QueueAnalyser>(sp =>
        {
            var sink = sp.GetRequiredService<IDataSink>();
            var svc = sp.GetRequiredService<ISvcBusService>();
            var log = sp.GetRequiredService<ILogger<QueueAnalyser>>();
            var serviceBusSettings = sp.GetRequiredService<IOptions<ServiceBusRepoSettings>>();
            return new QueueAnalyser(sink, svc, serviceBusSettings, log);
        });

        services.AddTransient<ServiceBusClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ServiceBusRepoSettings>>().Value;
            var envName = config["EnvironmentName"];

            return envName == "LOCAL"
                ? new ServiceBusClient(settings.ServiceBusConnectionString, new ServiceBusClientOptions
                {
                    TransportType = ServiceBusTransportType.AmqpWebSockets
                })
                : new ServiceBusClient(settings.ServiceBusConnectionString);
        });

        return services;
    }
}