using Auth0.ManagementApi.Models.Actions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Logging.Models;
using SIL.Transcriber.Data;


namespace SIL.Logging.Repositories
{
    public class ParatextTokenHistoryRepository(
    ITargetedFields targetedFields, LoggingDbContextResolver contextResolver,
        IResourceGraph resourceGraph, IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor
            ) : LoggingDbContextRepository<Paratexttokenhistory>(targetedFields, contextResolver, resourceGraph, resourceFactory,
            constraintProviders, loggerFactory, resourceDefinitionAccessor)
    {
        protected readonly LoggingDbContext logDbContext = (LoggingDbContext)contextResolver.GetContext();

        public Paratexttokenhistory Create(Paratexttokenhistory entity)
        {
            logDbContext.Paratexttokenhistory.Add(entity);
            logDbContext.SaveChanges();
            return entity;
        }
    }
}