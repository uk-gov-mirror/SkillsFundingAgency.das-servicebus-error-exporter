using System;
using Microsoft.Extensions.Logging;
using SFA.DAS.Tools.AnalyseErrorQueues.Engine;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Functions
{
    public class AnalyseErrorQueuesToBlob
    {
        private readonly IAnalyseQueuesBase _analyser;
        private readonly ILogger<AnalyseErrorQueuesToBlob> _logger;

        public AnalyseErrorQueuesToBlob(IAnalyseQueuesBase analyser, ILogger<AnalyseErrorQueuesToBlob> logger)
        {
            _analyser = analyser ?? throw new Exception("Analyser is null");
            _logger = logger ?? throw new Exception("Logger is null");
        }

        [Function("AnalyseErrorQueuesToBlob")]
#if DEBUG
        public async Task Run([TimerTrigger("0 */1 * * * *",RunOnStartup = true)]TimerInfo timer)
#else
        public async Task Run([TimerTrigger("0 0 0 * * *")]TimerInfo timer)
#endif
        {
             _logger.LogInformation($"AnalyseErrorQueueToBlob function executed at: {DateTime.Now}");

            if (_analyser == null)
            {
                _logger.LogError("_analyser is null. Skipping execution.");
                return;
            }

            await _analyser.Run();
        }
    }
}
