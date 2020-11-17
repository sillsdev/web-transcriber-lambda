using SIL.Transcriber.Models;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public interface IOfflineDataService
    {
        FileResponse ExportProject(int id, int start);
        FileResponse ImportFileURL(string sFile);
        Task<FileResponse> ImportFileAsync(int projectid, string filename);
        Task<FileResponse> ImportFileAsync(string filename);

    }
}