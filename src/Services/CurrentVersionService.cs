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
            if (version.Contains("beta") && cvs.Where(v => v.DesktopVersion.Contains("beta")).Any())
                    return cvs.Where(v => v.DesktopVersion.Contains("beta")).First();
            else
                return cvs.FirstOrDefault();
        }
    }
}
