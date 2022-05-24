using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Linq;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class UserVersionRepository : BaseRepository<UserVersion>
    {
        private readonly CurrentVersionService CVService;
        public UserVersionRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            CurrentVersionService cvService
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            CVService = cvService;
        }
        public UserVersion CreateOrUpdate(string version, string env)
        {
            try {
                string fp = dbContext.Fingerprint();
            
                UserVersion? uv = GetAll()?.Where(x => x.LastModifiedOrigin == fp).FirstOrDefault();
                if (uv != null)
                {
                    uv.DesktopVersion = version;
                    uv.Environment = env;
                    dbContext.Update(uv);
                }
                else
                {
                    uv = new UserVersion
                    {
                        DesktopVersion = version,
                        Environment = env,
                    };
                    dbContext.UserVersions.Add(uv);
                }
                dbContext.SaveChanges();
                CurrentVersion cv = CVService.GetVersion(version);
                uv.DesktopVersion = cv.DesktopVersion;
                uv.DateUpdated = cv.DateUpdated;
                return uv;
            } catch (Exception ex)
            {
                Logger.LogError(ex, "userversion get");
                return new UserVersion
                {
                    DesktopVersion = "1",
                    Environment = env,
                    DateUpdated = new DateTime(2000, 1, 1)
                };
            }

        }
        public override IQueryable<UserVersion> FromCurrentUser(IQueryable<UserVersion>? entities = null)
        {
            return base.GetAll();
        }
        protected override IQueryable<UserVersion> FromProjectList(IQueryable<UserVersion>? entities, string idList)
        {
            return entities ?? GetAll();
        }
    }
}
