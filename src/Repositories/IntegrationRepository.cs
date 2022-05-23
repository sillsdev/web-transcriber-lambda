using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Repositories
{
    public class IntegrationRepository : BaseRepository<Integration>
    {
        public IntegrationRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
        }
        protected override IQueryable<Integration> FromCurrentUser(QueryLayer? layer = null)
        {
            return GetAll();
        }
        protected override IQueryable<Integration> FromProjectList(QueryLayer layer, string idList)
        {
            return GetAll();
        }
    }
}
