using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService
{
    public class psvDataSink : IDataSink
    {
        public async Task SinkMessages(string envName, string queueName, IEnumerable<sbMessageModel> messages)
        {
           
            var sb = new StringBuilder();
            sb.AppendLine("MessageId | TimeOfFailure | ExceptionType | OriginatingEndpoint | ProcessingEndpoint | EnclosedMessageTypes | ExceptionMessage | Stack Trace");
            foreach (var msg in messages)
            {
                var psvLine = $"{msg.MessageId} |  {msg.TimeOfFailure} | {msg.ExceptionType} | {msg.OriginatingEndpoint} | {msg.ProcessingEndpoint} | {msg.EnclosedMessageTypes} | {msg.ExceptionMessage} | {msg.StackTrace}";
                sb.AppendLine(psvLine);
            }

            File.WriteAllText($".\\{envName}.{queueName}.PeekedMessages.psv", sb.ToString());
        }
    }
}
