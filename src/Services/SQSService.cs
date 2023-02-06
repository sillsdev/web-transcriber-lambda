using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using System.Net;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    public class SQSExportMessageBody
    {
        public string Folder { get; set; } = "";
        public string PTFFile { get; set; } = "";
        public int ProjectId { get; set; }
        public int Start { get; set; }
    }
    public class SQSService: ISQSService
    {
        private readonly IAmazonSQS _client;
        protected ILogger<SQSService> Logger { get; set; }

        public SQSService(IAmazonSQS client, ILoggerFactory loggerFactory)
        {
            _client = client;
            this.Logger = loggerFactory.CreateLogger<SQSService>();
        }
        public string SendExportMessage(int projectId, string folder, string ptfFile, int start)
        {
            SQSExportMessageBody body = new()
            {
                PTFFile = ptfFile,
                Start = start,
                ProjectId = projectId,
                Folder = folder
            };
            return SendMessage(JsonConvert.SerializeObject(body), $"{projectId}_{start}", projectId.ToString());
        }
        public string SendMessage(string body, string? deDup,  string? groupId)
        {
            string url = GetVarOrDefault("SIL_TR_EXPORT_QUEUE", "https://sqs.us-east-1.amazonaws.com/620141372223/APMExportQueue-dev.fifo");
            try
            {
                SendMessageRequest sendMessageRequest = new ()
                {
                    QueueUrl = url,
                    MessageBody = body,
                    MessageGroupId = groupId,
                    MessageDeduplicationId = string.Concat(deDup ?? "", Guid.NewGuid().ToString())
                };
                Console.WriteLine("***** body {0} groupId {1} deDup {2}", sendMessageRequest.MessageBody, sendMessageRequest.MessageGroupId, sendMessageRequest.MessageDeduplicationId);
                SendMessageResponse sqsSend = _client.SendMessageAsync(sendMessageRequest).Result;
                return sqsSend.MessageId;
            }
            catch (AmazonSQSException ex)
            {
                Console.WriteLine(ex);
                return "error";
            }
        }
      
    }
}
