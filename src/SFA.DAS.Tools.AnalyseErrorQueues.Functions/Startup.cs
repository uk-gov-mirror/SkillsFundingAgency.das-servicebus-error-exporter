using SFA.DAS.Tools.AnalyseErrorQueues.Engine;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.SvcBusService;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using SFA.DAS.Tools.AnalyseErrorQueues.Functions.Infrastructure;
using SFA.DAS.Tools.AnalyseErrorQueues.Functions;

[assembly: FunctionsStartup(typeof(Startup))]

namespace SFA.DAS.Tools.AnalyseErrorQueues.Functions
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var sp = builder.Services.BuildServiceProvider();

            var executionContextOptions = builder.Services.BuildServiceProvider()
                .GetService<IOptions<ExecutionContextOptions>>()
                .Value
            ;
            var appDirectory = executionContextOptions.AppDirectory;
            var configurationService = sp.GetService<IConfiguration>();
            var config = LoadConfiguration(appDirectory, configurationService);

            builder.Services.AddTransient(s => new BlobDataSink(config, s.GetRequiredService<ILogger<BlobDataSink>>()));
            builder.Services.AddTransient(s => new laDataSink(config, s.GetRequiredService<ILogger<laDataSink>>()));
            builder.Services.AddTransient<ISvcBusService, SvcBusService>(s => new SvcBusService(config, s.GetRequiredService<ILogger<SvcBusService>>()));

            builder.Services.AddTransient<IAnalyseQueues, QueueAnalyser>(s =>
            {
                var sink = s.GetRequiredService<laDataSink>();
                var svc = s.GetRequiredService<ISvcBusService>();
                var log = s.GetRequiredService<ILogger<QueueAnalyser>>();

                return new QueueAnalyser(sink, svc, config, log);
            });

            builder.Services.AddTransient<IAnalyseQueuesBase, QueueAnalyser>(s =>
            {
                var sink = s.GetRequiredService<BlobDataSink>();
                var svc = s.GetRequiredService<ISvcBusService>();
                var log = s.GetRequiredService<ILogger<QueueAnalyser>>();

                return new QueueAnalyser(sink, svc, config, log);
            });
        }

        public static IConfiguration LoadConfiguration(string appDirectory, IConfiguration configurationService)
        {
            Trace.WriteLine($"appDirectory: {appDirectory}");
            var builder = new ConfigurationBuilder()
                .SetBasePath(appDirectory)
                .AddJsonFile("local.settings.json",
                    optional: true,
                    reloadOnChange: true)
                .AddJsonFile("appsettings.json",
                    optional: true,
                    reloadOnChange: true)
                .AddJsonFile("local.appsettings.json",
                    optional: true,
                    reloadOnChange: true)
                .AddConfiguration(configurationService)
                .AddEnvironmentVariables()
                .AddAzureTableStorageConfiguration(
                    configurationService["ConfigurationStorageConnectionString"],
                    configurationService["AppName"],
                    configurationService["EnvironmentName"],
                    "1.0", "SFA.DAS.Tools.AnalyseErrorQueues.Functions");
            return builder.Build();
        }
    }
}