﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class CurrentversionService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Currentversion> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentversionRepository cvRepo,
        AppDbContextResolver contextResolver
        ) : BaseService<Currentversion>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor,
            cvRepo
            )
    {
        public CurrentversionRepository CurrentversionRepository = cvRepo;
        protected readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();

        public Currentversion StoreVersion(string version)
        {
            return CurrentversionRepository.CreateOrUpdate(version);
        }

        public Currentversion GetVersion(string version)
        {
            Microsoft.EntityFrameworkCore.DbSet<Currentversion>? cvs = dbContext.Currentversions;
            Currentversion? cv = null;
            if (version.Contains("beta"))
                cv = cvs?.Where(v =>
                        (v.DesktopVersion ?? "").Contains("beta")
                        || (v.DesktopVersion ?? "").Contains("rc")
                )
                    .FirstOrDefault();
            else if (version.Contains("rc"))
                cv = cvs?.Where(v => (v.DesktopVersion ?? "").Contains("rc")).FirstOrDefault();
            return cv ?? cvs?.FirstOrDefault() ?? new();
        }
    }
}
