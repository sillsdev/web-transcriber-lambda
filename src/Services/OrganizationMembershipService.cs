using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;


namespace SIL.Transcriber.Services
{
    public class OrganizationMembershipService : BaseArchiveService<OrganizationMembership>
    {
        public OrganizationMembershipService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<OrganizationMembership> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, OrganizationMembershipRepository repository) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, 
                  resourceChangeTracker, resourceDefinitionAccessor,repository)
        {
        }
    }
}
