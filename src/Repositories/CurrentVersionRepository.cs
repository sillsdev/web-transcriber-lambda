using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Repositories
{
    public class CurrentversionRepository : BaseRepository<Currentversion>
    {
         public CurrentversionRepository(
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
        public Currentversion CreateOrUpdate(string version)
        {
            Currentversion? cv = GetAll().FirstOrDefault();
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
                cv = new Currentversion
                {
                    DesktopVersion = version
                };
                dbContext.Currentversions.Add(cv);
                dbContext.SaveChanges();
            }
            return cv;
        }
        public override IQueryable<Currentversion> FromCurrentUser(IQueryable<Currentversion>? entities = null) 
        { 
            return entities ?? GetAll(); 
        }
        protected override IQueryable<Currentversion> FromProjectList(IQueryable<Currentversion>? entities, string idList) 
        { 
            return entities??GetAll(); 
        }


    }
}
