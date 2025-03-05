using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class AllowedFileset
    {
#pragma warning disable IDE1006 // Naming Styles
        public string type { get; set; } = "";
        public string language { get; set; } = "";
        public string licensor { get; set; } = "";
        public string fileset_id { get; set; } = "";
#pragma warning restore IDE1006 // Naming Styles
    }
    public class BibleBrainFilesetService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Biblebrainfileset> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        BibleBrainFilesetRepository repository
        ) : BaseService<Biblebrainfileset>(
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
        private readonly BibleBrainFilesetRepository _repo = repository;

        public Biblebrainfileset? PostAllowed(AllowedFileset fileset)
        {
            return _repo.PostAllowed(fileset);
        }
        public Biblebrainfileset? GetFileset(string fileset_id)
        {
            return _repo.GetFileset(fileset_id);
        }
        public async Task<Biblebrainfileset?> UpdateTiming(string filesetid)
        {
            Biblebrainfileset? fs = _repo.GetFileset(filesetid);
            if (fs == null)
            {
                return null;
            }
            fs.Timing = true;
            await _repo.UpdateAsync(fs, fs, new CancellationToken());
            return fs;
        }
    }
}
