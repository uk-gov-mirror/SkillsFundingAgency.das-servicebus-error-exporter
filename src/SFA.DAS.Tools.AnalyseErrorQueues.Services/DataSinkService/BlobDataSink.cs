using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain.Configuration;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService
{
    public class BlobDataSink : IDataSink
    {
        private readonly BlobDataSinkSettings _config;
        private readonly ILogger _logger;

        public BlobDataSink(IOptions<BlobDataSinkSettings> config, ILogger<BlobDataSink> logger)
        {
            _config = config.Value ?? throw new Exception("config is null");
            _logger = logger ?? throw new Exception("logger is null");
        }

        public async Task SinkMessages(string envName, string queueName, IEnumerable<sbMessageModel> messages)
        {
            var connString = _config.StorageConnectionString;
            var blobContainerName = _config.StorageContainerName;

#if DEBUG
            if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            {
                _logger.LogDebug($"connString: {connString}");
                _logger.LogDebug($"connString: {blobContainerName}");
            }
#endif

            var sb = new StringBuilder();
            sb.AppendLine("MessageId | TimeOfFailure | ExceptionType | OriginatingEndpoint | ProcessingEndpoint | EnclosedMessageTypes | ExceptionMessage | Stack Trace | Raw");
            foreach (var msg in messages)
            {
                var psvLine = $"{msg.MessageId} |  {msg.TimeOfFailure} | {msg.ExceptionType} | {msg.OriginatingEndpoint} | {msg.ProcessingEndpoint} | {msg.EnclosedMessageTypes} | {msg.ExceptionMessage} | {msg.StackTrace} | {msg.RawMessage}";
                sb.AppendLine(psvLine);
            }

            try
            {
                var byteArr = Encoding.UTF8.GetBytes(sb.ToString());

                using (var stream = new MemoryStream(byteArr))
                {
                    var blobServiceClient = new BlobServiceClient(connString);
                    var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                    await blobContainerClient.CreateIfNotExistsAsync();

                    var blobClient = blobContainerClient.GetBlobClient($"{envName}.{queueName}.PeekedMessages.psv");

                    await blobClient.UploadAsync(stream, overwrite: true);

                    _logger.LogInformation("File Successfully uploaded");
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical($"Could not connect to storage: {ex.Message}");


            }
        }
    }
}
