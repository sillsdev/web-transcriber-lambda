using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services.Contracts
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
        Task<Fileresponse> ImportCopyFileIntoOrgAsync(int org, string filename, int start, string? mapKey);
        Task<Fileresponse> ImportCopyProjectAsync(bool neworg, int projectid, int start, string? newProjId);
        void RemoveCopyProject(string newProjId);
    }
}
