using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class UserVersionRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        CurrentversionService cvService
        ) : BaseRepository<Userversion>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        private readonly CurrentversionService CVService = cvService;

        public Userversion CreateOrUpdate(string version, string env)
        {
            try
            {
                string fp = dbContext.Fingerprint();

                Userversion? uv = GetAll()?.Where(x => x.LastModifiedOrigin == fp).FirstOrDefault();
                if (uv != null)
                {
                    uv.DesktopVersion = version;
                    uv.Environment = env;
                    _ = dbContext.Update(uv);
                }
                else
                {
                    uv = new Userversion { DesktopVersion = version, Environment = env, };
                    _ = dbContext.UserVersions.Add(uv);
                }
                _ = dbContext.SaveChanges();
                Currentversion cv = CVService.GetVersion(version);
                uv.DesktopVersion = cv.DesktopVersion;
                uv.DateUpdated = cv.DateUpdated;
                return uv;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "userversion get");
                return new Userversion
                {
                    DesktopVersion = "1",
                    Environment = env,
                    DateUpdated = new DateTime(2000, 1, 1)
                };
            }
        }

        public override IQueryable<Userversion> FromCurrentUser(
            IQueryable<Userversion>? entities = null
        )
        {
            return base.GetAll();
        }

        public override IQueryable<Userversion> FromProjectList(
            IQueryable<Userversion>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
