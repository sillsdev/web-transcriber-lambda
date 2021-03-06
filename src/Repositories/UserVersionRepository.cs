﻿using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Repositories
{
    public class UserVersionRepository : BaseRepository<UserVersion>
    {
        public UserVersionRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        public UserVersion CreateOrUpdate(string version, string env)
        {
            string fp = dbContext.GetFingerprint();

            UserVersion uv = Get().Where(x => x.LastModifiedOrigin == fp).FirstOrDefault();
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
            CurrentVersion cv = dbContext.CurrentVersions.FirstOrDefault();
            uv.DesktopVersion = cv.DesktopVersion;
            uv.DateUpdated = cv.DateUpdated;
            return uv;
        }
    }
}
