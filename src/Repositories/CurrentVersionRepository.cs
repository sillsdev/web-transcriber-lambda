using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class CurrentversionRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Currentversion>(
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
        public Currentversion CreateOrUpdate(string version)
        {
            Currentversion? cv = GetAll().FirstOrDefault();
            if (cv != null)
            {
                if (cv.DesktopVersion != version)
                {
                    cv.DesktopVersion = version;
                    _ = dbContext.Update(cv);
                    _ = dbContext.SaveChanges();
                }
            }
            else
            {
                cv = new Currentversion { DesktopVersion = version };
                _ = dbContext.Currentversions.Add(cv);
                _ = dbContext.SaveChanges();
            }
            return cv;
        }

        public override IQueryable<Currentversion> FromCurrentUser(
            IQueryable<Currentversion>? entities = null
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Currentversion> FromProjectList(
            IQueryable<Currentversion>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
