using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
namespace SIL.Transcriber.Repositories;

public class SharedResourceRepository : BaseRepository<Sharedresource>
{
    public SharedResourceRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
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
    }

    public IQueryable<Sharedresource> UsersSharedResources(IQueryable<Sharedresource> entities)
    {   //send them all
        return CurrentUser == null ? entities.Where(e => e.Id == -1) : entities.Where(e => !e.Archived);
    }

    public IQueryable<Sharedresource> ProjectSharedResources(
        IQueryable<Sharedresource> entities,
        string projectid
    )
    {
        //TODO get where (clusterid is null) + (clusterid is set and my org is in the cluster)
        return entities.Where(e => !e.Archived);
    }

    public IQueryable<Sharedresource> GetMine()
    {
        return FromCurrentUser().Include(o => o.ArtifactCategory).Include(o => o.Passage);
    }

    #region Overrides
    public override IQueryable<Sharedresource> FromCurrentUser(
        IQueryable<Sharedresource>? entities = null
    )
    {
        return UsersSharedResources(entities ?? GetAll());
    }

    public override IQueryable<Sharedresource> FromProjectList(
        IQueryable<Sharedresource>? entities,
        string idList
    )
    {
        return ProjectSharedResources(entities ?? GetAll(), idList);
    }
    #endregion
}

