using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories;

   public class SharedResourceReferenceRepository : BaseRepository<Sharedresourcereference>
    {
        private readonly SharedResourceRepository SharedResourceRepository;

        public SharedResourceReferenceRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SharedResourceRepository sharedResourceRepository
        )
            : base(
                targetedFields,
                contextResolver,
                resourceGraph,
                resourceFactory,
                constraintProviders,
                loggerFactory,
                resourceDefinitionAccessor,
                currentUserRepository
            )
        {
            SharedResourceRepository =
                sharedResourceRepository ?? throw new ArgumentNullException(nameof(SharedResourceRepository));
        }

        public IQueryable<Sharedresourcereference> UsersSharedResourceReferences(IQueryable<Sharedresourcereference> entities)
        {
            IQueryable<Sharedresource>? resources = SharedResourceRepository.UsersSharedResources(dbContext.Sharedresources.AsQueryable());
            return entities.Join(resources, o => o.SharedResourceId, r => r.Id, (o, r) => o);
        }

        public IQueryable<Sharedresourcereference> ProjectSharedResourceReferences(
            IQueryable<Sharedresourcereference> entities,
            string projectid
        )
        {
            IQueryable<Sharedresource>? resources = SharedResourceRepository.ProjectSharedResources(dbContext.Sharedresources.AsQueryable(), projectid);
            return entities.Join(resources, o => o.SharedResourceId, r => r.Id, (o, r) => o);
        }

        public IQueryable<Sharedresourcereference> GetMine()
        {
            return FromCurrentUser().Include(o => o.SharedResource);
        }

        #region Overrides
        public override IQueryable<Sharedresourcereference> FromCurrentUser(
            IQueryable<Sharedresourcereference>? entities = null
        )
        {
            return UsersSharedResourceReferences(entities ?? GetAll());
        }

        public override IQueryable<Sharedresourcereference> FromProjectList(
            IQueryable<Sharedresourcereference>? entities,
            string idList
        )
        {
            return ProjectSharedResourceReferences(entities ?? GetAll(), idList);
        }
        #endregion
    }

