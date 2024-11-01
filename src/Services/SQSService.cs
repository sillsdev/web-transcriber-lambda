using Amazon.SQS;
using Amazon.SQS.Model;
using Newtonsoft.Json;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    public class SQSBibleBrainGeneralResourceMessageBody
    {
        public string Type { get; } = "general";
        public string FilesetId { get; set; } = "";
        public string? Codec { get; set; }  //mp3, opus
        public string Book { get; set; } = "";
        public int Chapter { get; set; }
        public int PlanId { get; set; }
        public string Lang { get; set; } = "";
        public string Desc { get; set; } = "";
        public int ArtifactTypeId { get; set; }
        public int? ArtifactCategoryId { get; set; }
        public string Token { get; set; } = "";
    }
    public class SQSBibleBrainResourceMessageBody
    {
        public string Type { get; } = "resource";
        public string FilesetId { get; set; } = "";
        public string Book { get; set; } = "";
        public int Chapter { get; set; }
        public int? PassageId {  get; set; }
        public int SectionId { get; set; }
        public int PlanId { get; set; }
        public string Lang { get; set; } = "";
        public string Desc { get; set; } = "";
        public int Startverse { get; set; }
        public int Endverse { get; set; }
        public int Sequence { get; set; }
        public int ArtifactTypeId { get; set; }
        public int? ArtifactCategoryId { get; set; }
        public int OrgWorkflowStepId { get; set; }
        public string Token { get; set; } = "";
    }
    
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
        public async Task<int> MessageCount(string queue)
        {
            GetQueueAttributesResponse attributes = await _client.GetQueueAttributesAsync(new 
                GetQueueAttributesRequest
                {
                    QueueUrl = queue,
                    AttributeNames = new List<string> { "ApproximateNumberOfMessages" }
                });

            return attributes.Attributes.TryGetValue("ApproximateNumberOfMessages", out string? messageCount)
                ? int.TryParse(messageCount, out int count) ? count : 0
                : throw new Exception("Couldn't retrieve message count");
        }
        public async Task<int> BBMessageCount()
        {
            string queue = GetVarOrThrow("SIL_TR_BIBLEBRAIN_QUEUE");
            return await MessageCount(queue);
        }

        public string SendBBResourceMessage(string filesetId,
                                            string book,
                                            int chapter,
                                            int? psgId,
                                            int sectionId,
                                            int planId,
                                            string lang,
                                            string desc,
                                            int startverse,
                                            int endverse,
                                            int seq,
                                            int artifactTypeId,
                                            int? artifactCategoryId,
                                            int orgWorkflowStepId,
                                            string token)
        {
            SQSBibleBrainResourceMessageBody body = new()
            {
                FilesetId= filesetId,
                Book= book,
                Chapter= chapter,
                PassageId= psgId,
                SectionId= sectionId,
                PlanId= planId,
                Lang= lang,
                Desc= desc,
                Startverse= startverse,
                Endverse= endverse,
                Sequence= seq,
                ArtifactTypeId= artifactTypeId,
                ArtifactCategoryId = artifactCategoryId,
                OrgWorkflowStepId= orgWorkflowStepId,
                Token = token
            };
            string url = GetVarOrThrow("SIL_TR_BIBLEBRAIN_QUEUE");
            string msg = JsonConvert.SerializeObject(body);
            return SendMessage(url, msg, $"{filesetId}{sectionId}{psgId??0}", filesetId.ToString());
        }
        public string SendBBGeneralMessage(string filesetId,
                                   string? codec,
                                   string book,
                                   int chapter,
                                   int planId,
                                   string lang,
                                   string desc,
                                   int artifactTypeId,
                                   int? artifactCategoryId,
                                   string token)
        {
            SQSBibleBrainGeneralResourceMessageBody body = new()
            {
                FilesetId= filesetId,
                Codec= codec,
                Book= book,
                Chapter= chapter,
                PlanId= planId,
                Lang= lang,
                Desc= desc,
                ArtifactTypeId= artifactTypeId,
                ArtifactCategoryId = artifactCategoryId,
                Token = token
            };
            string url = GetVarOrThrow("SIL_TR_BIBLEBRAIN_QUEUE");
            string msg = JsonConvert.SerializeObject(body);
            return SendMessage(url, msg, $"{filesetId}_gr", filesetId.ToString());
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
            string url = GetVarOrDefault("SIL_TR_EXPORT_QUEUE", "https://sqs.us-east-1.amazonaws.com/620141372223/APMExportQueue-dev.fifo");
            return SendMessage(url, JsonConvert.SerializeObject(body), $"{projectId}_{start}", projectId.ToString());
        }
        public string SendMessage(string url, string body, string? deDup,  string? groupId)
        {
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
