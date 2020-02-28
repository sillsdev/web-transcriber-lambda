using SIL.Transcriber.Models;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public interface IOfflineDataService
    {
        FileResponse ExportProject(int id);
        FileResponse ExportOrganization(int id);
        FileResponse ImportFileURL(string sFile);
        Task<FileResponse> ImportFileAsync(string filename);
    }
}