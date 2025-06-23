using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.SvcBusService;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain.Configuration;
using Azure.Messaging.ServiceBus.Administration;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Services.SvcBusService
{
    public class SvcBusService : ISvcBusService
    {
        private readonly ServiceBusRepoSettings _config;
        private readonly ILogger _logger;
        private readonly ServiceBusClient _serviceBusClient;

        public SvcBusService(ServiceBusClient serviceBusClient, IOptions<ServiceBusRepoSettings> config, ILogger<SvcBusService> logger)
        {
            _serviceBusClient = serviceBusClient;
            _config = config.Value ?? throw new Exception("config is null");
            _logger = logger ?? throw new Exception("logger is null");
        }

        public async Task<IEnumerable<string>> GetErrorQueuesAsync()
        {

            var managementClient = new ServiceBusAdministrationClient(_config.ServiceBusConnectionString);
            var errorQueues = new List<string>();

            var regexTimeout = TimeSpan.FromSeconds(5); 
            var queueSelectionRegex = new Regex(_config.QueueSelectionRegex, RegexOptions.None, regexTimeout);


            await foreach (var queue in managementClient.GetQueuesAsync())
            {
                if (queueSelectionRegex.IsMatch(queue.Name))
                {
                    errorQueues.Add(queue.Name);
                }
            }
#if DEBUG
            _logger.LogDebug("Error Queues:");
            foreach (var queue in errorQueues)
            {
                _logger.LogDebug(queue);
            }
#endif
            return errorQueues;
        }

        public async Task<IList<sbMessageModel>> PeekMessages(string queueName)
        {
            var batchSize = _config.PeekMessageBatchSize;
            var notifyBatchSize = _config.NotifyUIBatchSize;


            var messageReceiver = _serviceBusClient.CreateReceiver(queueName);

#if DEBUG
            _logger.LogDebug($"ServiceBusConnectionString: {_config.ServiceBusConnectionString}");
            _logger.LogDebug($"PeekMessageBatchSize: {batchSize}");
#endif

            int totalMessages = 0;
            var formattedMessages = new List<sbMessageModel>();

            var peekedMessages = await messageReceiver.PeekMessagesAsync(batchSize);

            _logger.LogDebug($"Peeked Message Count: {peekedMessages.Count}");

            while (peekedMessages?.Count > 0)
            {
                foreach (var msg in peekedMessages)
                {
                    var messageModel = FormatMsgToLog(msg);
                    totalMessages++;
                    if (totalMessages % notifyBatchSize == 0)
                        _logger.LogDebug($"    {queueName} - processed: {totalMessages}");

                    formattedMessages.Add(messageModel);
                }
                peekedMessages = await messageReceiver.PeekMessagesAsync(batchSize);
            }
            await messageReceiver.CloseAsync();

            return formattedMessages;
        }

        private sbMessageModel FormatMsgToLog(ServiceBusReceivedMessage msg)
        {
            var messageModel = new sbMessageModel();

            string GetStringValue(string key) =>
            msg.ApplicationProperties.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

            string GetCrLfToTildeValue(string key) =>
            GetStringValue(key).CrLfToTilde();

            string GetSplitFirstValue(string key) =>
            GetStringValue(key).Split(',').FirstOrDefault() ?? string.Empty;

            if (msg.ApplicationProperties.TryGetValue("NServiceBus.ExceptionInfo.Message", out var exceptionMessage))
            {
                // This is an nServiceBusFailure.
                messageModel.ExceptionMessage = exceptionMessage?.ToString()?.CrLfToTilde() ?? string.Empty;
                messageModel.EnclosedMessageTypes = GetSplitFirstValue("NServiceBus.EnclosedMessageTypes");

                messageModel.MessageId = GetStringValue("NServiceBus.MessageId");
                messageModel.TimeOfFailure = GetStringValue("NServiceBus.TimeOfFailure");
                messageModel.ExceptionType = GetStringValue("NServiceBus.ExceptionInfo.ExceptionType");
                messageModel.OriginatingEndpoint = GetStringValue("NServiceBus.OriginatingEndpoint");
                messageModel.ProcessingEndpoint = GetStringValue("NServiceBus.ProcessingEndpoint");
                messageModel.StackTrace = GetCrLfToTildeValue("NServiceBus.ExceptionInfo.StackTrace");
            }
            else if (msg.ApplicationProperties.TryGetValue("DeadLetterReason", out exceptionMessage))
            {
                messageModel.ExceptionMessage = exceptionMessage?.ToString()?.CrLfToTilde() ?? string.Empty;
                messageModel.EnclosedMessageTypes = GetSplitFirstValue("NServiceBus.EnclosedMessageTypes");

                messageModel.MessageId = GetStringValue("NServiceBus.MessageId");
                messageModel.TimeOfFailure = GetStringValue("NServiceBus.TimeSent");
                messageModel.ExceptionType = "Unknown";
                messageModel.OriginatingEndpoint = GetStringValue("NServiceBus.OriginatingEndpoint");
                messageModel.ProcessingEndpoint = "Unknown";
                messageModel.StackTrace = string.Empty;
            }

#if DEBUG
    // When developing, I want to be able to use as simple a message as possible but still see some information in the output,
    // so I will just grab the message body and output it raw.
    else
    {
        _logger.LogDebug($"msg.Body: {Encoding.UTF8.GetString(msg.Body.ToArray())}");
        messageModel.RawMessage = Encoding.UTF8.GetString(msg.Body.ToArray());
    }
#endif
            return messageModel;
            }

    }
}
