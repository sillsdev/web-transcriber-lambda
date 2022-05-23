using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;


namespace SIL.Transcriber.Services
{
    public class BaseService<TResource> : JsonApiResourceService<TResource, int>
        where TResource : BaseModel
    {
        protected IResourceRepositoryAccessor RepositoryAssessor { get; }
        protected IJsonApiOptions Options { get; }
        protected ILogger<TResource> Logger { get; set; }


        public BaseService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor)
        {
            Options = options;
            Logger = loggerFactory.CreateLogger<TResource>();
            RepositoryAssessor = repositoryAccessor;
        }
        public virtual IEnumerable<TResource> GetChanges(int currentuser, string origin, DateTime since, int project)
        {
            if (currentuser > 0)
            {
                return GetChanges(GetAsync(new CancellationToken(false)).Result, currentuser, origin, since);
            }
            else
            {
                throw new Exception("I thought this wasn't used");
                //return GetChanges(GetByProjAsync(project).Result, currentuser, origin, since);
            }
        }

        public IEnumerable<TResource> GetChanges(IReadOnlyCollection<TResource> entities, int currentuser, string origin, DateTime since)
        {
            if (entities == null) return new List<TResource>();
            if (currentuser > 0)
                return entities.Where(p => (p.LastModifiedBy != currentuser || p.LastModifiedOrigin != origin) && p.DateUpdated > since);
            return entities.Where(p => p.LastModifiedOrigin != origin && p.DateUpdated > since);
        }

        public override async Task DeleteAsync(int id, CancellationToken ct)
        {
            TResource existing = await base.GetAsync(id,ct);
            if (existing == null)
            {
                return;
            }
            await base.DeleteAsync(id,ct);
        }
        public override async Task<TResource?> CreateAsync(TResource resource, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<TResource>? all = await base.GetAsync(cancellationToken);
            //orbit sometimes sends two in a row...see if we already know about this one
            TResource? x = all.Where(t => t.DateCreated == resource.DateCreated && t.LastModifiedByUser?.Id == resource.LastModifiedByUser?.Id).FirstOrDefault();
            if (x == null)
                return await base.CreateAsync(resource, cancellationToken);
            return x;
        }
    }
}

