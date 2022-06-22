using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class RoleRepository : BaseRepository<Role>
    {
        public RoleRepository(
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
            ) { }

        public override IQueryable<Role> FromCurrentUser(IQueryable<Role>? entities = null)
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Role> FromProjectList(IQueryable<Role>? entities, string idList)
        {
            return entities ?? GetAll();
        }
    }
}
