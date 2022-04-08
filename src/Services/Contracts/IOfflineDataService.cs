using SIL.Transcriber.Models;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public interface IOfflineDataService
    {
        FileResponse ExportProjectPTF(int id, int start);
        FileResponse ExportProjectAudio(int projectid, string artifactType, string ids, int start);
        FileResponse ExportBurrito(int projectid, string ids, int start);
        FileResponse ImportFileURL(string sFile);
        Task<FileResponse> ImportFileAsync(int projectid, string filename);
        Task<FileResponse> ImportFileAsync(string filename);

    }
}