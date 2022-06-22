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
    public class DatachangesRepository : BaseRepository<Datachanges>
    {
        public DatachangesRepository(
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

        public override IQueryable<Datachanges> FromCurrentUser(
            IQueryable<Datachanges>? entities = null
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Datachanges> FromProjectList(
            IQueryable<Datachanges>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
