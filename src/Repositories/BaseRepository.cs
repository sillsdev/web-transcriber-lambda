using System;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;


namespace SIL.Transcriber.Repositories
{
    public class BaseRepository<TEntity> : BaseRepository<TEntity, int>, IEntityRepository<TEntity> where TEntity : BaseModel
    {
        public BaseRepository(ILoggerFactory loggerFactory,
                              IJsonApiContext jsonApiContext,
                              CurrentUserRepository currentUserRepository,
                              //EntityHooksService<TEntity, int> statusUpdateService,
                              IDbContextResolver contextResolver)
            : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
    }

    public class BaseRepository<TEntity, TId> : DefaultEntityRepository<TEntity, TId>
        where TEntity : BaseModel, IIdentifiable<TId>
    {
        protected readonly DbSet<TEntity> dbSet;
        protected readonly CurrentUserRepository currentUserRepository;
        //protected readonly EntityHooksService<TEntity, TId> statusUpdateService;
        protected readonly AppDbContext dbContext;
        protected ILogger<TEntity> Logger { get; set; }

        public BaseRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            this.dbSet = contextResolver.GetDbSet<TEntity>();
            this.currentUserRepository = currentUserRepository;
            this.Logger = loggerFactory.CreateLogger<TEntity>();
        }

        public User CurrentUser {
            get {
                return currentUserRepository.GetCurrentUser().Result;
            }
        }
        /*
        public override async Task<TEntity> UpdateAsync(TId id, TEntity entity)
        {
            var retval = await base.UpdateAsync(id, entity);
            statusUpdateService.DidUpdate(retval);
            return retval;
        }
        */
        public override async Task<TEntity> CreateAsync(TEntity entity)
        {
            try
            {
                TEntity x = base.Get().Where(t => t.DateCreated == entity.DateCreated && t.LastModifiedBy == entity.LastModifiedBy).FirstOrDefault();
                if (x == null)
                    return await base.CreateAsync(entity);
                return x;
            }
            catch (DbUpdateException ex)
            {
                throw ex;  //does this go back to my controller?  Nope...eaten by JsonApiExceptionFilter.  TODO: Figure out a way to capture it and return a 400 instead
            }
        }
      /*
        public override async Task<bool> DeleteAsync(TId id)
        {
            var entity = await GetAsync(id);
            var retval = await base.DeleteAsync(id);
            if (retval)
            {
                statusUpdateService.DidDelete(entity);
            }
            return retval;
        }
        */
    }
}
