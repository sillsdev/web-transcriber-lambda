using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Services
{
    public class CurrentVersionService : BaseService<CurrentVersion>
    {
        public CurrentVersionRepository CurrentVersionRepository;
        protected readonly AppDbContext dbContext;
        public CurrentVersionService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<CurrentVersion> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, CurrentVersionRepository cvRepo, AppDbContextResolver contextResolver) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
            CurrentVersionRepository = cvRepo;
            dbContext = (AppDbContext)contextResolver.GetContext();
        }

        public CurrentVersion StoreVersion(string version)
        {
            return CurrentVersionRepository.CreateOrUpdate(version);
        }
        public CurrentVersion GetVersion(string version)
        {
            Microsoft.EntityFrameworkCore.DbSet<CurrentVersion>? cvs = dbContext.CurrentVersions;
            CurrentVersion? cv = null;
            if (version.Contains("beta"))
                cv = cvs?.Where(v => (v.DesktopVersion??"").Contains("beta")|| (v.DesktopVersion ?? "").Contains("rc")).FirstOrDefault();
            else if (version.Contains("rc"))
                cv = cvs?.Where(v => (v.DesktopVersion??"").Contains("rc")).FirstOrDefault();
            if (cv != null) return cv;
            return cvs?.FirstOrDefault() ?? new();
        }
    }
}
