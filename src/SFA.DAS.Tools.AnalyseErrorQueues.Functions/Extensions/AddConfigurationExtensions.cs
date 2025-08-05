using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SFA.DAS.Configuration.AzureTableStorage;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Functions.Extensions
{
    public static class ConfigurationExtensions
    {
        public static IConfigurationBuilder AddDasConfiguration(
            this IConfigurationBuilder configBuilder,
            IHostEnvironment environment)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("local.settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var configNames = config["ConfigNames"]?.Split(',') ?? Array.Empty<string>();
            var envName = config["EnvironmentName"];
            var storageConnStr = config["ConfigurationStorageConnectionString"];

            configBuilder
                .SetBasePath(environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();

            if (!string.IsNullOrWhiteSpace(storageConnStr))
            {
                configBuilder.AddAzureTableStorage(options =>
                {
                    options.ConfigurationKeys = configNames;
                    options.StorageConnectionString = storageConnStr;
                    options.EnvironmentName = envName;
                    options.PreFixConfigurationKeys = false;
                });
            }

            return configBuilder;
        }
    }
}