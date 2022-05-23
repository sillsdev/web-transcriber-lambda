using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class DataChangesRepository : BaseRepository<DataChanges>
    {
        public DataChangesRepository(
               ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory,resourceDefinitionAccessor, currentUserRepository)
        {
        }
        protected override IQueryable<DataChanges> FromCurrentUser(QueryLayer layer)
        {
            return base.GetAll();
        }
        protected override IQueryable<DataChanges> FromProjectList(QueryLayer layer, string idList)
        {
            return base.GetAll();
        }
    }
}