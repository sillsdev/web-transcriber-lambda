﻿using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Repositories
{
    public class CurrentVersionRepository : BaseRepository<CurrentVersion>
    {
         public CurrentVersionRepository(
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
        public CurrentVersion CreateOrUpdate(string version)
        {
            CurrentVersion? cv = GetAll().FirstOrDefault();
            if (cv != null)
            {
                if (cv.DesktopVersion != version)
                {
                    cv.DesktopVersion = version;
                    dbContext.Update(cv);
                    dbContext.SaveChanges();
                }
            }
            else
            {
                cv = new CurrentVersion
                {
                    DesktopVersion = version
                };
                dbContext.CurrentVersions.Add(cv);
                dbContext.SaveChanges();
            }
            return cv;
        }
        protected override IQueryable<CurrentVersion> FromCurrentUser(QueryLayer layer) 
        { 
            return base.GetAll(); 
        }
        protected override IQueryable<CurrentVersion> FromProjectList(QueryLayer layer, string idList) 
        { 
            return base.GetAll(); 
        }


    }
}
