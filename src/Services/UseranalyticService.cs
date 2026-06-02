using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class UseranalyticService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Useranalytic> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        AppDbContextResolver contextResolver,
        GeoIpService geoIpService
        ) : JsonApiResourceService<Useranalytic, int>(
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
        private readonly GeoIpService _geoIpService = geoIpService;
        private readonly ILogger Logger = loggerFactory.CreateLogger<UseranalyticService>();

        public override async Task<Useranalytic?> CreateAsync(Useranalytic resource, CancellationToken cancellationToken)
        {
            Useranalytic? existing = await _dbContext.Useranalytics
                .FirstOrDefaultAsync(u => u.UserId == resource.UserId && u.Year == resource.Year && u.Month == resource.Month, cancellationToken);

            if (existing != null)
            {
                return existing;
            }

            _ = _dbContext.Useranalytics.Add(resource);
            _ = await _dbContext.SaveChangesAsync(cancellationToken);
            return resource;
        }

        public async Task<(Useranalytic Useranalytic, Countryanalytic Countryanalytic)> TrackAsync(
            int userId,
            CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;
            string country = await _geoIpService.GetCountryAsync(cancellationToken);
            Logger.LogInformation("TrackAsync start for UserId={UserId}, Year={Year}, Month={Month}, Country={Country}", userId, now.Year, now.Month, country);

            await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                Useranalytic useranalytic = await _dbContext.Useranalytics
                    .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, cancellationToken)
                    ?? _dbContext.Useranalytics.Add(new Useranalytic
                    {
                        UserId = userId,
                        Year = now.Year,
                        Month = now.Month
                    }).Entity;

                Countryanalytic countryanalytic = await _dbContext.Countryanalytics
                    .FirstOrDefaultAsync(c => c.Country == country && c.Year == now.Year && c.Month == now.Month, cancellationToken)
                    ?? _dbContext.Countryanalytics.Add(new Countryanalytic
                    {
                        Country = country,
                        Year = now.Year,
                        Month = now.Month
                    }).Entity;

                _ = await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                Logger.LogInformation("TrackAsync committed for UserId={UserId}; returned country={Country}", userId, country);
                return (useranalytic, countryanalytic);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "TrackAsync failed for UserId={UserId}", userId);
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}