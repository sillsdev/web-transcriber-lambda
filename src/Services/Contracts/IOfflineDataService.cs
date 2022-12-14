using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface IOfflineDataService
    {
        Fileresponse ExportProjectPTF(int id, int start);
        Fileresponse ExportProjectAudio(int projectid, string artifactType, string? ids, int start, bool elan = false);
        Fileresponse ExportBurrito(int projectid, string? ids, int start);
        Fileresponse ImportFileURL(string sFile);
        Task<Fileresponse> ImportFileAsync(int projectid, string filename);
        Task<Fileresponse> ImportFileAsync(string filename);
    }
}
