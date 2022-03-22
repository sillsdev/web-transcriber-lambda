using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace SIL.Transcriber.Services
{
    public class CurrentVersionService : BaseService<CurrentVersion>
    {
        public CurrentVersionService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            CurrentVersionRepository repository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, repository, loggerFactory)
        {
        }

        public CurrentVersion StoreVersion(string version)
        {
            return ((CurrentVersionRepository)MyRepository).CreateOrUpdate(version);
        }
        public CurrentVersion GetVersion(string version)
        {
            IEnumerable<CurrentVersion> cvs = GetAsync().Result;
            CurrentVersion cv = null;
            if (version.Contains("beta"))
                cv = cvs.Where(v => v.DesktopVersion.Contains("beta")|| v.DesktopVersion.Contains("rc")).FirstOrDefault();
            else if (version.Contains("rc"))
                cv = cvs.Where(v => v.DesktopVersion.Contains("rc")).FirstOrDefault();
            if (cv != null) return cv;
            return cvs.FirstOrDefault();
                
        }
    }
}
