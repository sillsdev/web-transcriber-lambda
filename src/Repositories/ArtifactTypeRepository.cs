using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class ArtifactTypeRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Artifacttype>(
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
        private readonly OrganizationRepository OrganizationRepository = organizationRepository;

        //at one time we thought the users would be able to add artifacttype, but it's really an 
        //internal thing that we need control of, so ignore the organization
        public IQueryable<Artifacttype> UsersArtifactTypes(IQueryable<Artifacttype> entities)
        {
            return entities;
        }

        //at one time we thought the users would be able to add artifacttype, but it's really an 
        //internal thing that we need control of, so ignore the organization
        public IQueryable<Artifacttype> ProjectArtifactTypes(
            IQueryable<Artifacttype> entities,
            string projectid
        )
        {
            return entities;
        }

        #region Overrides
        public override IQueryable<Artifacttype> FromProjectList(
            IQueryable<Artifacttype>? entities,
            string idList
        )
        {
            return ProjectArtifactTypes(entities ?? GetAll(), idList);
        }

        public override IQueryable<Artifacttype> FromCurrentUser(
            IQueryable<Artifacttype>? entities = null
        )
        {
            return UsersArtifactTypes(entities ?? GetAll());
        }
        #endregion
    }
}
