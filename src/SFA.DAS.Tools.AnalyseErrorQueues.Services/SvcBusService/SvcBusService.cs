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
#if DEBUG
            _logger.LogDebug($"Peeked Message Count: {peekedMessages.Count}");
#endif

            while (peekedMessages?.Count > 0)
            {
                foreach (var msg in peekedMessages)
                {
                    var messageModel = FormatMsgToLog(msg);
                    totalMessages++;
                    if (totalMessages % notifyBatchSize == 0)
#if DEBUG
                        _logger.LogDebug($"    {queueName} - processed: {totalMessages}");
#endif
                    
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
                msg.ApplicationProperties.TryGetValue(key, out var value) ?
                Truncate(value?.ToString()) : string.Empty;

            string GetCrLfToTildeValue(string key) =>
                Truncate(GetStringValue(key).CrLfToTilde());

            string GetSplitFirstValue(string key) =>
                Truncate(GetStringValue(key).Split(',').FirstOrDefault() ?? string.Empty);

            try
            {
                if (msg.ApplicationProperties.TryGetValue("NServiceBus.ExceptionInfo.Message", out var exceptionMessage))
                {
                    messageModel.ExceptionMessage = Truncate(exceptionMessage?.ToString()?.CrLfToTilde());
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
                    messageModel.ExceptionMessage = Truncate(exceptionMessage?.ToString()?.CrLfToTilde());
                    messageModel.EnclosedMessageTypes = GetSplitFirstValue("NServiceBus.EnclosedMessageTypes");
                    messageModel.MessageId = GetStringValue("NServiceBus.MessageId");
                    messageModel.TimeOfFailure = GetStringValue("NServiceBus.TimeSent");
                    messageModel.ExceptionType = "Unknown";
                    messageModel.OriginatingEndpoint = GetStringValue("NServiceBus.OriginatingEndpoint");
                    messageModel.ProcessingEndpoint = "Unknown";
                    messageModel.StackTrace = string.Empty;
                }

#if DEBUG
                else
                {
                    var bodyString = Truncate(Encoding.UTF8.GetString(msg.Body.ToArray()));
                    _logger.LogDebug($"msg.Body: {bodyString}");
                    messageModel.RawMessage = bodyString;
                }
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting message to log.");
                messageModel.ExceptionMessage = "Error formatting message: " + ex.Message;
            }

            return messageModel;
        }
        private string Truncate(string input, int maxLength = 50000)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            if (input.Length > maxLength)
            {
                _logger?.LogWarning("Message body exceeds {OriginalLength} characters. Truncating to {TruncateLength}.", maxLength, maxLength);
                return input.Substring(0, maxLength) + "...";
            }

            return input;
        }
    }
}
