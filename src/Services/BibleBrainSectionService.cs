using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class BibleBrainSectionService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Biblebrainsection> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        BibleBrainSectionRepository repository
        ) : BaseService<Biblebrainsection>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor,
            repository
            )
    {
        public override async Task<Biblebrainsection?> CreateAsync(
                                                        Biblebrainsection section,
                                                        CancellationToken cancellationToken
        )
        {
            Biblebrainsection? existing = Repo.Get().Where(s => s.BibleId == section.BibleId && s.BookId == section.BookId && s.StartChapter == section.StartChapter && s.StartVerse == section.StartVerse).FirstOrDefault();
            if (existing != null)
            {
                if (existing.EndChapter == section.EndChapter && existing.EndVerse == section.EndVerse)
                {
                    if (existing.BookTitle != section.BookTitle)
                    {
                        existing.BookTitle = section.BookTitle;
                        return await UpdateAsync(existing.Id, existing, cancellationToken);
                    }
                    return existing;
                }
                await Repo.DeleteAsync(existing, existing.Id, cancellationToken);
            }
            return await base.CreateAsync(section, cancellationToken);
        }
    }
}

