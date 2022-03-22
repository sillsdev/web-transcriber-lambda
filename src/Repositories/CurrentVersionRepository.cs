using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Repositories
{
    public class CurrentVersionRepository : BaseRepository<CurrentVersion>
    {
         public CurrentVersionRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        public CurrentVersion CreateOrUpdate(string version)
        {
            CurrentVersion cv = Get().FirstOrDefault();
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
    }
}
