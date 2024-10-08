﻿using SIL.Paratext.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public interface IParatextService
    {
        UserSecret ParatextLogin();
        Task<IReadOnlyList<ParatextOrg>> GetOrgsAsync(UserSecret userSecret);
        Task<IReadOnlyList<ParatextProject>?> GetProjectsAsync(UserSecret userSecret);
        Task<IReadOnlyList<ParatextProject>?> GetProjectsAsync(UserSecret userSecret, string languageTag);
        string? GetParatextUsername(UserSecret userSecret);
        Task<Attempt<string?>> TryGetProjectRoleAsync(UserSecret userSecret, string paratextId);
        Task<Attempt<string?>> TryGetUserEmailsAsync(UserSecret userSecret, string email);
        Task<IReadOnlyList<string>?> GetBooksAsync(UserSecret userSecret, string projectId);
        Task<string> GetBookTextAsync(UserSecret userSecret, string projectId, string bookId);
        Task<string> UpdateBookTextAsync(UserSecret userSecret, string projectId, string bookId,
            string revision, string usxText);
        Task<string> GetNotesAsync(UserSecret userSecret, string projectId, string bookId);
        Task<string> UpdateNotesAsync(UserSecret userSecret, string projectId, string notesText);
        Task<int> ProjectPassagesToSyncCountAsync(int projectId, int artifactTypeId);
        int PlanPassagesToSyncCount(int planId, int artifactTypeId);
        int PassageToSyncCount(int passageid, int artifactTypeId);
        Task<List<ParatextChapter>> SyncPlanAsync(UserSecret userSecret, int planId, int artifactTypeId);
        Task<List<ParatextChapter>> SyncProjectAsync(UserSecret userSecret, int projectId, int artifactTypeId);
        Task<List<ParatextChapter>> SyncPassageAsync(UserSecret userSecret, int passageId, int artifactTypeId);
        Task<string?> PassageTextAsync(int passageId, int type);
        Task<bool> GetCanPublishAsync(UserSecret userSecret);
    }
}
