namespace SFA.DAS.Tools.AnalyseErrorQueues.Domain.Configuration
{
    public class ApplicationConfiguration
    {
        public LADataSinkSettings LADataSinkSettings { get; set; }
        public ServiceBusRepoSettings ServiceBusRepoSettings { get; set; }

        public BlobDataSinkSettings BlobDataSinkSettings { get; set; }
    }

    public class LADataSinkSettings
    {
        public string workspaceId { get; set; }
        public string sharedKey { get; set; }
        public string LogName { get; set; }
        public string TimeStampField { get; set; }
        public int ChunkSize { get; set; }
    }

    public class ServiceBusRepoSettings
    {
        public string ServiceBusConnectionString { get; set; }
        public string QueueSelectionRegex { get; set; }
        public string EnvName { get; set; }
        public int PeekMessageBatchSize { get; set; }
        public int NotifyUIBatchSize { get; set; }
    }

    public class BlobDataSinkSettings
    {
        public string StorageConnectionString { get; set; }
        public string StorageContainerName { get; set; }
    }
}