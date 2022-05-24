﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public interface IBaseArchiveService<T1, T2> where T1 : BaseModel
    {
    }
    public class BaseArchiveService<TResource> : BaseService<TResource>
         where TResource : BaseModel, IArchive
    {
        private readonly BaseRepository<TResource> repo;
        public BaseArchiveService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<TResource> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, BaseRepository<TResource> baseRepo) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor,baseRepo)
        {
        }

        public override IEnumerable<TResource> GetChanges(IQueryable<TResource> entities, int currentuser, string origin, DateTime since, int project)
        {
            return base.GetChanges(entities, currentuser, origin, since, project).Where(t => !t.Archived).ToList();
        }

        public IEnumerable<TResource> GetDeletedSince(IQueryable<TResource> entities, int currentuser, string origin, DateTime since)
        {
            return base.GetChanges(Repo.FromCurrentUser(entities), currentuser, origin, since).Where(t => t.Archived); ;
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
            TResource? existing = await base.GetAsync(id, new CancellationToken());
            if (existing?.Archived??true)
            {
                throw new Exception("Entity has been deleted. Unable to update.");
            }
            return await base.UpdateAsync(id, entity, cancellationToken);
        }
    }
}