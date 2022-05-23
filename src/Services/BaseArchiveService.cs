using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface IBaseArchiveService<T1, T2> where T1 : BaseModel
    {
    }
    public class BaseArchiveService<TResource> : BaseService<TResource>
         where TResource : BaseModel, IArchive
    {
 
        public BaseArchiveService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor)
        {
        }

        public override IEnumerable<TResource> GetChanges(int currentuser, string origin, DateTime since, int project)
        {
            IEnumerable<TResource>? entities = base.GetChanges(currentuser, origin, since, project);
            return entities.Where(t => !t.Archived);
        }

        public IEnumerable<TResource> GetDeletedSince(int currentuser, string origin, DateTime since)
        {
            //TODO 05/12 RemoveScopedToCurrentUser(options);                 //avoid the current user thing...
            IReadOnlyCollection <TResource> entities = base.GetAsync(new CancellationToken()).Result; //avoid the archived check...
            return base.GetChanges(entities, currentuser, origin, since).Where(t => t.Archived); ;
        }
        public override async Task<IReadOnlyCollection<TResource>> GetAsync(CancellationToken cancellationToken)
        {
            //return unarchived
            IReadOnlyCollection<TResource> entities = await base.GetAsync(cancellationToken);
            if (typeof(IArchive).IsAssignableFrom(typeof(TResource)))
            {
                entities = entities.Where(t => !t.Archived).ToList();
            }
            return entities;
        }
        public override async Task<TResource?> UpdateAsync(int id, TResource entity, CancellationToken cancellationToken)
        {
            //return unarchived
            TResource existing = await base.GetAsync(id, new CancellationToken());
            if (existing.Archived)
            {
                throw new Exception("Entity has been deleted. Unable to update.");
            }
            return await base.UpdateAsync(id, entity, cancellationToken);
        }
    }
}