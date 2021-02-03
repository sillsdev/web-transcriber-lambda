using JsonApiDotNetCore.Services;
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
        public UserVersion CreateOrUpdate(string version)
        {
            string fp = dbContext.GetFingerprint();

            UserVersion uv = Get().Where(x => x.LastModifiedOrigin == fp).FirstOrDefault();
            if (uv != null)
            {
                if (uv.DesktopVersion != version)
                {
                    uv.DesktopVersion = version;
                    dbContext.Update(uv);
                    dbContext.SaveChanges();
                }
            }
            else
            {
                uv = new UserVersion
                {
                    DesktopVersion = version
                };
                dbContext.UserVersions.Add(uv);
                dbContext.SaveChanges();
            }
            CurrentVersion cv = dbContext.CurrentVersions.FirstOrDefault();
            uv.DesktopVersion = cv.DesktopVersion;
            uv.DateUpdated = cv.DateUpdated;
            return uv;
        }
    }
}
