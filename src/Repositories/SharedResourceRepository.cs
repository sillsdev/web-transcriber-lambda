using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
namespace SIL.Transcriber.Repositories;

public class SharedResourceRepository(
    ITargetedFields targetedFields,
    AppDbContextResolver contextResolver,
    IResourceGraph resourceGraph,
    IResourceFactory resourceFactory,
    IEnumerable<IQueryConstraintProvider> constraintProviders,
    ILoggerFactory loggerFactory,
    IResourceDefinitionAccessor resourceDefinitionAccessor,
    CurrentUserRepository currentUserRepository,
        MediafileRepository mediafileRepository
    ) : BaseRepository<Sharedresource>(
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
    readonly private MediafileRepository MediafileRepository = mediafileRepository;
    public override async Task UpdateAsync(Sharedresource resourceFromRequest, Sharedresource resourceFromDatabase, CancellationToken cancellationToken)
    {
        int? titleMedia = resourceFromRequest.TitleMediafileId ?? resourceFromDatabase.TitleMediafileId;
        if (titleMedia != null) //always do titles 
            await MediafileRepository.Publish((int)titleMedia, "{\"Public\": \"true\"}", true);
        await base.UpdateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
    }
    public IQueryable<Sharedresource> UsersSharedResources(IQueryable<Sharedresource> entities)
    {   //send them all
        return CurrentUser == null ? entities.Where(e => e.Id == -1) : entities;
    }

    public IQueryable<Sharedresource> ProjectSharedResources(
        IQueryable<Sharedresource> entities,
        string projectid
    )
    {
        //TODO get where (clusterid is null) + (clusterid is set and my org is in the cluster)
        return entities;
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

