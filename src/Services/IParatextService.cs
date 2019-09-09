using System.Collections.Generic;
using System.Threading.Tasks;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{ 
    public interface IParatextService
    {
        Task<UserSecret> ParatextLogin();
        Task<IReadOnlyList<ParatextProject>> GetProjectsAsync(UserSecret userSecret);
        string GetParatextUsername(UserSecret userSecret);
        Task<Attempt<string>> TryGetProjectRoleAsync(UserSecret userSecret, string paratextId);
        Task<IReadOnlyList<string>> GetBooksAsync(UserSecret userSecret, string projectId);
        Task<string> GetBookTextAsync(UserSecret userSecret, string projectId, string bookId);
        Task<string> UpdateBookTextAsync(UserSecret userSecret, string projectId, string bookId,
            string revision, string usxText);
        Task<string> GetNotesAsync(UserSecret userSecret, string projectId, string bookId);
        Task<string> UpdateNotesAsync(UserSecret userSecret, string projectId, string notesText);
    }
}
