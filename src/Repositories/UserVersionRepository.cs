using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Linq;

namespace SIL.Transcriber.Repositories
{
    public class UserVersionRepository : BaseRepository<UserVersion>
    {
        private CurrentVersionService CVService;
        public UserVersionRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver,
            CurrentVersionService cvService
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            CVService = cvService;
        }
        public UserVersion CreateOrUpdate(string version, string env)
        {
            string fp = dbContext.GetFingerprint();
            try
            {
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
    }
}
