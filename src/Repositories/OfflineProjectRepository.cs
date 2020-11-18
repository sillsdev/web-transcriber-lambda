using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Data;


namespace SIL.Transcriber.Repositories
{
    public class OfflineProjectRepository : BaseRepository<OfflineProject>
    {
        public OfflineProjectRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          AppDbContextResolver contextResolver
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        private IQueryable<OfflineProject> UsersOfflineProjects(IQueryable<OfflineProject> entities)
        {
            return entities;
        }
        public override IQueryable<OfflineProject> Filter(IQueryable<OfflineProject> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersOfflineProjects(entities);
            }
            return base.Filter(entities, filterQuery);
        }
    }
}
