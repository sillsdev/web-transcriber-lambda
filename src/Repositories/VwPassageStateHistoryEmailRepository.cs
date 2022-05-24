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
    public class VwPassageStateHistoryEmailRepository : BaseRepository<VwPassageStateHistoryEmail>
    {
        public VwPassageStateHistoryEmailRepository(
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
        public override IQueryable<VwPassageStateHistoryEmail> FromCurrentUser(IQueryable<VwPassageStateHistoryEmail>? entities = null)
        {
            return entities ?? GetAll();
        }
        protected override IQueryable<VwPassageStateHistoryEmail> FromProjectList(IQueryable<VwPassageStateHistoryEmail>? entities, string idList)
        {
            return entities ?? GetAll();
        }
    }
}
