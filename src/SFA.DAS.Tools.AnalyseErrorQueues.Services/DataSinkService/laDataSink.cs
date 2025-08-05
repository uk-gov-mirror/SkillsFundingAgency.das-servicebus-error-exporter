using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain;
using Microsoft.Extensions.Options;
using SFA.DAS.Tools.AnalyseErrorQueues.Domain.Configuration;

namespace SFA.DAS.Tools.AnalyseErrorQueues.Services.DataSinkService
{
    public class laDataSink : IDataSink
    {
		private string datestring = string.Empty;
		private readonly LADataSinkSettings _config;
		private readonly ILogger _logger;

		public laDataSink(IOptions<LADataSinkSettings> config, ILogger<laDataSink> logger)
		{
			_config = config.Value ?? throw new Exception("config is null");
			_logger = logger ?? throw new Exception("logger is null");
		}

        public async Task SinkMessages(string envName, string queueName, IEnumerable<sbMessageModel> messages)
        {
            // Create a hash for the API signature
            datestring = DateTime.UtcNow.ToString("r");

            // Create aggregate messages to send to azure log analytics.
            var errorsByReceivingDomain =
                from m in messages
                group m by new {m.ProcessingEndpoint, m.OriginatingEndpoint, m.EnclosedMessageTypes, m.ExceptionType} into summaryGroup
                select new
                {
                    Environment = envName,
                    Queue = queueName,
                    ProcessingEndpoint = summaryGroup.Key.ProcessingEndpoint,
                    OriginatingEndpoint = summaryGroup.Key.OriginatingEndpoint,
                    EnclosedMessageTypes = summaryGroup.Key.EnclosedMessageTypes,
                    ExceptionType = summaryGroup.Key.ExceptionType,
                    Count = summaryGroup.Count(),
                };

            // Send to log analytics in small(ish) batches.  We dont want to send 100s of messages in one go.
            var chunkSize = _config.ChunkSize;
            var sharedKey = _config.sharedKey;
            var workspaceId = _config.workspaceId;
            var sendBatches = errorsByReceivingDomain.ChunkBy(chunkSize);

            _logger.LogInformation($"chunkSize: {chunkSize}");
            _logger.LogInformation($"sendBatches: {sendBatches}");

#if DEBUG
            _logger.LogDebug($"sharedKey: {sharedKey}");
            _logger.LogDebug($"workspaceId: {workspaceId}");
#endif

            foreach (var batch in sendBatches)
            {
                var jsonList = JArray.FromObject(batch);
                var json = jsonList.ToString(Formatting.None);

                _logger.LogInformation($"json: {json}");

                var jsonBytes = Encoding.UTF8.GetBytes(json);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, sharedKey);
                string signature = $"SharedKey {workspaceId}:{hashedString}";

                PostData(signature, datestring, json);
            }
        }

        public static string BuildSignature(string message, string secret)
		{
			var encoding = new System.Text.ASCIIEncoding();
			byte[] keyByte = Convert.FromBase64String(secret);
			byte[] messageBytes = encoding.GetBytes(message);
			using (var hmacsha256 = new HMACSHA256(keyByte))
			{
				byte[] hash = hmacsha256.ComputeHash(messageBytes);
				return Convert.ToBase64String(hash);
			}
		}

		// Send a request to the POST API endpoint
		public void PostData(string signature, string date, string json)
		{
            var logName = _config.LogName;
            var timestampField = _config.TimeStampField;
            var workspaceId = _config.workspaceId;
            string url = $"https://{workspaceId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

            _logger.LogInformation($"logName: {logName}");
            _logger.LogInformation($"timestampField: {timestampField}");

#if DEBUG
            _logger.LogDebug($"workspaceId: {workspaceId}");
            _logger.LogDebug($"url: {url}");
#endif

            try
			{
				HttpClient client = new System.Net.Http.HttpClient();
				client.DefaultRequestHeaders.Add("Accept", "application/json");
				client.DefaultRequestHeaders.Add("Log-Type", logName);
				client.DefaultRequestHeaders.Add("Authorization", signature);
				client.DefaultRequestHeaders.Add("x-ms-date", date);
				client.DefaultRequestHeaders.Add("time-generated-field", timestampField);

				HttpContent httpContent = new StringContent(json, Encoding.UTF8);
				httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
				Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

				HttpContent responseContent = response.Result.Content;
				string result = responseContent.ReadAsStringAsync().Result;
            }
			catch (Exception ex)
			{
				_logger.LogCritical($"Error POSTing to Log Analytics: {ex.ToString()}");
			}
		}
		        
    }
}
