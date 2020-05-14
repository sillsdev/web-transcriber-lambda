using System;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Serialization;
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
        #region MultipleData //orgdata, projdata
        protected string InitData()
        {
            return "{\"data\":[";
        }
        protected string FinishData()
        {
            return "]}";
        }
        protected bool CheckAdd(int check, object entity, DateTime dtBail, IJsonApiSerializer jsonApiSerializer, ref int start, ref string data)
        {
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail) return false;
            if (start <= check)
            {
                string thisdata = jsonApiSerializer.Serialize(entity);
                if (data.Length + thisdata.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisdata;
                start++;
            }
            return true;
        }
        #endregion
    }
}
