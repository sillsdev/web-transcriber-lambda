using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class CountryanalyticService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Countryanalytic> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        AppDbContextResolver contextResolver
        ) : JsonApiResourceService<Countryanalytic, int>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor
            )
    {
        private readonly AppDbContext _dbContext = (AppDbContext)contextResolver.GetContext();

        public override async Task<Countryanalytic?> CreateAsync(Countryanalytic resource, CancellationToken cancellationToken)
        {
            Countryanalytic? existing = await _dbContext.Countryanalytics
                .FirstOrDefaultAsync(c => c.Country == resource.Country && c.Year == resource.Year && c.Month == resource.Month, cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            _ = _dbContext.Countryanalytics.Add(resource);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);
            return resource;
        }
    }
}