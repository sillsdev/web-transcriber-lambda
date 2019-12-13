using System;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class BaseRepository<TEntity> : BaseRepository<TEntity, int>, IEntityRepository<TEntity>
        where TEntity : class, IIdentifiable<int>
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
        where TEntity : class, IIdentifiable<TId>
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
            //SJH EntityHooksService<TEntity, TId> statusUpdateService,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            this.dbSet = contextResolver.GetDbSet<TEntity>();
            this.currentUserRepository = currentUserRepository;
            this.Logger = loggerFactory.CreateLogger<TEntity>();
            //SJH this.statusUpdateService = statusUpdateService;
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
                return  await base.CreateAsync(entity);
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
