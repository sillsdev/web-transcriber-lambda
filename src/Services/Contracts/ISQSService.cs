using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface ISQSService
    {
        public Task<int> MessageCount(string queue);
        public Task<int> BBMessageCount();
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
                                          string token);
        public  string SendBBGeneralMessage(string filesetId,
                                  string? codec,
                                  string book,
                                  int chapter,
                                  int planId,
                                  string lang,
                                  string desc,
                                  int artifactTypeId,
                                  int? artifactCategoryId,
                                  string token);
        public string SendExportMessage(int projectId,string folder, string ptfFile, int start);
        public string SendMessage(string url, string body, string? deDup, string? groupId);
    }
}
