using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface ISQSService
    {
        public string SendExportMessage(int projectId,string folder, string ptfFile, int start);
        public string SendMessage(string body, string? deDup, string? groupId);
    }
}
