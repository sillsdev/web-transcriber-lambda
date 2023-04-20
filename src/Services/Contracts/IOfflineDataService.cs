using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface IOfflineDataService
    {
        Fileresponse ExportProjectPTF(int id, int start);
        Fileresponse ExportProjectAudio(int projectid, string artifactType, string? ids, int start, bool elan, string? nameTemplate);
        Fileresponse ExportBurrito(int projectid, string? ids, int start);
        Fileresponse ImportFileURL(string sFile);
        Task<Fileresponse> ImportFileAsync(int projectid, string filename, int start);
        Task<Fileresponse> ImportSyncFileAsync(string filename, int file, int start);
        Task<Fileresponse> ImportCopyFileAsync(bool neworg, string filename);
        Task<Fileresponse> ImportCopyProjectAsync(bool neworg, int projectid, int start, int? newProjId);
        void RemoveCopyProject(int newProjId);
    }
}
