using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class BaseService<TResource> : JsonApiResourceService<TResource, int>
        where TResource : BaseModel
    {
        protected ILogger<TResource> Logger { get; set; }
        protected readonly BaseRepository<TResource> Repo;

        public BaseService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            BaseRepository<TResource> baseRepo
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor
            )
        {
            Logger = loggerFactory.CreateLogger<TResource>();
            Repo = baseRepo;
        }
        //GetAsync will apply FromCurrentUser - so do that first to see if the id is in that result set
#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member.
        public override async Task<TResource?> GetAsync(int id, CancellationToken cancellationToken)
#pragma warning restore CS8609 // Nullability of reference types in return type doesn't match overridden member.
        {
            return (await base.GetAsync(cancellationToken)).SingleOrDefault(g => g.Id == id);
        }

        public virtual IEnumerable<TResource> GetChanges(
            IQueryable<TResource> entities,
            int currentuser,
            string origin,
            DateTime since,
            int project, int startId
        )
        {
            IEnumerable<TResource>? changes = currentuser > 0
                ? GetChanges(Repo.FromCurrentUser(entities), currentuser, origin, since)
                : GetChanges(Repo.FromProjectList(entities, project.ToString()), currentuser, origin, since);
            return startId > 0 ? changes.OrderBy(r => r.Id).Where(e => e.Id >= startId) : changes.OrderBy(r => r.Id);
        }

        public IEnumerable<TResource> GetChanges(
            IQueryable<TResource> entities,
            int currentuser,
            string origin,
            DateTime since
        )
        {
            return entities == null
                ? new List<TResource>()
                : currentuser > 0
                ? entities.Where(p =>
                        (p.LastModifiedBy != currentuser || p.LastModifiedOrigin != origin) &&
                        p.DateUpdated > since
                )
                : (IEnumerable<TResource>)entities.Where(p => p.LastModifiedOrigin != origin &&
                                                              p.DateUpdated > since);
        }

        public override async Task DeleteAsync(int id, CancellationToken ct)
        {
            TResource existing = await base.GetAsync(id, ct);
            if (existing != null)
                await base.DeleteAsync(id, ct);
        }

        public override async Task<TResource?> CreateAsync(
            TResource resource,
            CancellationToken cancellationToken
        )
        {
            
            //orbit sometimes sends two in a row...see if we already know about this one
            TResource? x = resource.DateCreated == null ? null : Repo.Get().Where(t =>
                        t.DateCreated == resource.DateCreated
                        && t.LastModifiedBy == resource.LastModifiedBy
                )
                .FirstOrDefault();
            return x ?? await base.CreateAsync(resource, cancellationToken);

        }
    }
}
