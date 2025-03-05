using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class BibleBrainFilesetRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Biblebrainfileset>(
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
        public IQueryable<Biblebrainfileset> UsersBibles(
            IQueryable<Biblebrainfileset> entities
        )
        {
            return entities;
        }

        public IQueryable<Biblebrainfileset> ProjectBibles(
            IQueryable<Biblebrainfileset> entities,
#pragma warning disable IDE0060 // Remove unused parameter
            string projectid
#pragma warning restore IDE0060 // Remove unused parameter
        )
        {
            return entities;
        }

        #region Overrides
        public override IQueryable<Biblebrainfileset> FromProjectList(
            IQueryable<Biblebrainfileset>? entities,
            string idList
        )
        {
            return ProjectBibles(entities ?? GetAll(), idList);
        }

        public override IQueryable<Biblebrainfileset> FromCurrentUser(
            IQueryable<Biblebrainfileset>? entities = null
        )
        {
            return UsersBibles(entities ?? GetAll());
        }
        #endregion

        public Biblebrainfileset PostAllowed(AllowedFileset fs)
        {
            if (dbContext.BibleBrainFilesets.Any(f => f.FilesetId == fs.fileset_id))
            {
                return dbContext.BibleBrainFilesets.First(f => f.FilesetId == fs.fileset_id);
            }
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Biblebrainfileset> newfs =
                dbContext.BibleBrainFilesets.Add(new Biblebrainfileset
                {
                    FilesetId = fs.fileset_id,
                    MediaType = fs.type,
                    Licensor = fs.licensor
                });
            dbContext.SaveChanges();
            return newfs.Entity;
        }
        public Biblebrainfileset? GetFileset(string fileset_id)
        {
            return dbContext.BibleBrainFilesets.FirstOrDefault(f => f.FilesetId == fileset_id);
        }
    }
}
