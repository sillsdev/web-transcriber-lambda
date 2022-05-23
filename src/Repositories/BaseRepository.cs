using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Queries;
//using SIL.Transcriber.Utility.Extensions.JSONAPI;
using JsonApiDotNetCore.Queries.Expressions;
using System.Text.Json;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using JsonApiDotNetCore.Serialization.Response;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Serialization.JsonConverters;

namespace SIL.Transcriber.Repositories
{
    public abstract class BaseRepository<TEntity> : BaseRepository<TEntity, int> where TEntity: BaseModel
    {

        public BaseRepository(ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
             )
            : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                  constraintProviders, loggerFactory, resourceDefinitionAccessor,  currentUserRepository)
        {
        }
    }
    
    public abstract class BaseRepository<TEntity, TId> : AppDbContextRepository<TEntity>
        where TEntity : BaseModel, IIdentifiable<TId>
    {
        protected readonly CurrentUserRepository CurrentUserRepository;
        protected readonly AppDbContext dbContext;
        protected ILogger<TEntity> Logger { get; set; }
        protected readonly IEnumerable<IQueryConstraintProvider> ConstraintProviders;
        protected readonly JsonSerializerOptions Options = new()
        {
           // WriteIndented = true,
            //PropertyNamingPolicy = new CamelToDashNamingPolicy(),
        };

        public BaseRepository(ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory,resourceDefinitionAccessor)
        {
            dbContext = (AppDbContext)contextResolver.GetContext();
            CurrentUserRepository = currentUserRepository;
            Logger = loggerFactory.CreateLogger<TEntity>();
            ConstraintProviders = constraintProviders;
            Options.Converters.Add(new ResourceObjectConverter(resourceGraph));
        }
        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction BeginTransaction()
        {
            return dbContext.Database.BeginTransaction();
        }
        public User? CurrentUser {
            get {
                return CurrentUserRepository.GetCurrentUser();
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
        protected bool CheckAdd(int check, IEnumerable<object> entities, DateTime dtBail,  ref int start, ref string data)
        {
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail) return false;
            string test = JsonSerializer.Serialize(new Orgdata { StartIndex = 4, DateCreated = DateTime.Now.ToUniversalTime() }, Options);
            if (start <= check)
            {
                string thisdata = InitData();
                foreach(object entity in entities)
                {
                    thisdata += JsonSerializer.Serialize(entity, Options);
                }

                if (data.Length + thisdata.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisdata;
                start++;
            }
            return true;
        }
        #endregion

        #region filters
        public IQueryable<TEntity> FromIdList(string idList)
        {
            string[] ids = idList.Replace("'", "").Split("|");
            return base.GetAll().Where(e => ids.Any(i => i == e.Id.ToString()));
        }
        protected abstract IQueryable<TEntity> FromCurrentUser(QueryLayer layer); //force this one
        protected abstract IQueryable<TEntity> FromProjectList(QueryLayer layer, string idList);
        protected virtual IQueryable<TEntity> FromStartIndex(QueryLayer layer, string startIndex, string version = "", string projectid = "")
        {
            return base.ApplyQueryLayer(layer);
        }
        protected virtual IQueryable<TEntity> FromPlan(QueryLayer layer, string planId)
        {
            return base.ApplyQueryLayer(layer);
        }
        /*
        protected virtual IQueryable<TEntity> FromVersion(QueryLayer layer, string version, string projectid = "")
        {
            return base.ApplyQueryLayer(layer);
        }
        */
        protected override IQueryable<TEntity> ApplyQueryLayer(QueryLayer layer)
        {
            ExpressionInScope[] expressions = ConstraintProviders
                                        .SelectMany(provider => provider.GetConstraints())
                                        .Where(expressionInScope => expressionInScope.Scope == null).ToArray();
            foreach(ExpressionInScope ex in expressions)
            {
                /* multiple filters on the request will be or'd together */
                /* note this is not an all purpose solution, but one very specific to our case
                 * we only use multiples on orgdata,projdata */
                if (ex.Expression.GetType().IsAssignableFrom(typeof(LogicalExpression)))
                {
                    LogicalExpression logex = (LogicalExpression)ex.Expression;
                    IReadOnlyCollection<FilterExpression> terms = logex.Terms;
                    string projectid = "", startid = "", version = "";
                    foreach(FilterExpression term in terms)
                    {
                        if (term.Field() == FilterConstants.PROJECT_SEARCH_TERM)
                            projectid = term.Value().Replace("'", "");
                        else if (term.Field() == FilterConstants.DATA_START_INDEX)
                            startid = term.Value().Replace("'", "");
                        else if (term.Field() == FilterConstants.JSONFILTER)
                            version = term.Value().Replace("'", "");
                    }
                    if (startid != "")
                        return FromStartIndex(layer,startid,version, projectid);
                }
                else
                    switch (ex.Field())
                    {
                        case FilterConstants.IDLIST:
                            return FromIdList(ex.Value());
                        case FilterConstants.ALLOWED_CURRENTUSER:
                            return FromCurrentUser(layer);
                        case FilterConstants.PROJECT_LIST:
                            return FromProjectList(layer, ex.Value().Replace("'", ""));
                        case FilterConstants.PROJECT_SEARCH_TERM:
                            return FromProjectList(layer, ex.Value().Replace("'", ""));
                        case FilterConstants.DATA_START_INDEX:
                            return FromStartIndex(layer, ex.Value().Replace("'", ""), "");
                        case FilterConstants.PLANID:
                            return FromPlan(layer, ex.Value().Replace("'", ""));

                    }
            }
            return base.ApplyQueryLayer(layer);
        }
        #endregion

        public IEnumerable<TEntity> GetChanges(IQueryable<TEntity> entities, int currentuser, string origin, DateTime since)
        {
            if (entities == null) return new List<TEntity>();
            if (currentuser > 0)
                return entities.Where(p => (p.LastModifiedBy != currentuser || p.LastModifiedOrigin != origin) && p.DateUpdated > since);
            return entities.Where(p => p.LastModifiedOrigin != origin && p.DateUpdated > since);
        }
        public virtual IEnumerable<TEntity> GetChanges(int currentuser, string origin, DateTime since, int project)
        {
            IQueryable<TEntity>? entities = base.GetAll();
            if (typeof(IArchive).IsAssignableFrom(typeof(TEntity)))
            {
                entities = entities.Where(t => !((IArchive)t).Archived);
            }
            if (currentuser > 0)
            {
                return GetChanges(entities, currentuser, origin, since);
            }
            else
            {
                throw new Exception("I thought this wasn't used");
                //return GetChanges(GetByProjAsync(project).Result, currentuser, origin, since);
            }
        }
        /*
        public override Task CreateAsync(TEntity resourceFromRequest, TEntity resourceForDatabase, CancellationToken cancellationToken)
        {
            var all = base.GetAll();
            TEntity? x = all.Where(t => t.DateCreated == resourceFromRequest.DateCreated && t.LastModifiedByUser == resourceFromRequest.LastModifiedByUser).FirstOrDefault();
            if (x == null)
                return base.CreateAsync(resourceFromRequest, resourceForDatabase, cancellationToken);
            resourceForDatabase = x;
            return x as Task; //?? what should this be??
        }
        */
    }
    public class CamelToDashNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) {
            IEnumerable<string> parts = name.Select((x,index) =>
            {
                if (char.IsUpper(x)) return (index > 0 ? "-": "") + char.ToLower(x);
                return x.ToString();
            });
            return string.Join("", parts);
            //slow return Regex.Replace(name, @"([a-z])([A-Z])", "$1-$2").ToLower();
        }
    }
}
