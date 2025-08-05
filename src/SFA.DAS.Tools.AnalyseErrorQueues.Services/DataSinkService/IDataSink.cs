using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService
{
    public interface IDataSink
    {
        Task SinkMessages(string envName, string queueName, IEnumerable<sbMessageModel> messages);
    }
}
