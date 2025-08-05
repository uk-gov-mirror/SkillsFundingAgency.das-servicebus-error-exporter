using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.SvcBusService;
using SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService;
using System.Linq;
using Microsoft.Extensions.Options;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain.Configuration;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Engine
{
    public class QueueAnalyser : IAnalyseQueues
    {
        private readonly IDataSink _dataSink;
        private readonly ISvcBusService _svcBusSvc;
        private readonly ServiceBusRepoSettings _config;
        private readonly ILogger<QueueAnalyser> _logger;

        public QueueAnalyser(IDataSink dataSink, ISvcBusService svcBusSvc, IOptions<ServiceBusRepoSettings> config, ILogger<QueueAnalyser> logger)
        {
            _dataSink = dataSink ?? throw new Exception("data sink is null");
            _svcBusSvc = svcBusSvc ?? throw new Exception("service is null");
            _config = config.Value ?? throw new Exception("config is null");
            _logger = logger ?? throw new Exception("Logger is null");

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug($"_dataSink is: {_dataSink}");
                _logger.LogDebug($"_svcBusSvc is: {_svcBusSvc}");
                _logger.LogDebug($"_config is: {_config}");
                _logger.LogDebug($"_logger is: {_logger}");
            }
        }

        public async Task Run()
        {
            var timer = new Stopwatch();
            timer.Start();

            int totalMessages = 0;

            var errorQueues = await _svcBusSvc.GetErrorQueuesAsync();
            var envName = _config.EnvName;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation($"Processing Queues:");
                errorQueues.ToList().ForEach(q => _logger.LogInformation($"{q}"));
                _logger.LogInformation($"envName: {envName}");
            }

            foreach (var queueName in errorQueues)
            {
                // Register the queue message handler and receive messages in a loop
                _logger.LogInformation($"Processing messages for queue: {queueName}");
                var peekedMessages = await _svcBusSvc.PeekMessages(queueName);
                totalMessages += peekedMessages.Count;
                if (peekedMessages.Any())
                {
                    _dataSink.SinkMessages(envName, queueName, peekedMessages);
                }
                _logger.LogInformation($"Finished queue: {queueName} - processed: {peekedMessages.Count} messages");
            }

            timer.Stop();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("");
                _logger.LogInformation($"****** Complete. Processed {totalMessages} in {timer.Elapsed.TotalSeconds} seconds");
            }
        }
    }
}
