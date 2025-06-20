using System;
using Microsoft.Extensions.Logging;
using SFA.DAS.Tools.AnalyseErrorQueues.Engine;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Functions
{
    public class AnalyseErrorQueues
    {
        private readonly ILogger<AnalyseErrorQueues> _logger;
        private readonly IAnalyseQueues _analyser;

        public AnalyseErrorQueues(IAnalyseQueues analyser,  ILogger<AnalyseErrorQueues> logger)
        {
            _analyser = analyser;
            _logger = logger;
        }

        [Function("AnalyseErrorQueue")]
#if DEBUG
        public async Task Run(
            [TimerTrigger("0 */1 * * * *", RunOnStartup = true)] TimerInfo myTimer)

#else
        public async Task Run([TimerTrigger("0 0 0 * * *")]TimerInfo myTimer)
#endif
        {
            _logger.LogInformation($"AnalyseErrorQueue function executed at: {DateTime.Now}");
            
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

                await _analyser.Run();
            }
        }
    }
}
