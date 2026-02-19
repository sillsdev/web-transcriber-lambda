using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.JsonConverters;
using JsonApiDotNetCore.Serialization.Response;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serializers;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SIL.Transcriber.Repositories
{
    public class ResourceInfo(
        IResourceGraph resourceGraph,
        IJsonApiOptions options,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        IMetaBuilder metaBuilder
        )
    {
        public IResourceGraph ResourceGraph { get; set; } = resourceGraph;
        public IJsonApiOptions Options { get; set; } = options;
        public IResourceDefinitionAccessor ResourceDefinitionAccessor { get; set; } = resourceDefinitionAccessor;
        public IMetaBuilder MetaBuilder { get; set; } = metaBuilder;
    }
    public abstract class BaseRepository<TEntity>(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<TEntity, int>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
        where TEntity : BaseModel
    {
    }

    public abstract partial class BaseRepository<TEntity, TId> : AppDbContextRepository<TEntity>
        where TEntity : BaseModel, IIdentifiable<TId>
    {
        protected readonly IResourceDefinitionAccessor ResourceDefinitionAccessor;
        protected readonly IResourceGraph ResourceGraph;
        protected readonly CurrentUserRepository CurrentUserRepository;
        protected readonly AppDbContext dbContext;
        protected ILogger<TEntity> Logger { get; set; }
        protected readonly IEnumerable<IQueryConstraintProvider> ConstraintProviders;
        protected readonly JsonSerializerOptions Options =
            new()
            {
                // WriteIndented = true,
                //PropertyNamingPolicy = new CamelToDashNamingPolicy(),
            };

        public BaseRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
        )
            : base(
                targetedFields,
                contextResolver,
                resourceGraph,
                resourceFactory,
                constraintProviders,
                loggerFactory,
                resourceDefinitionAccessor
            )
        {
            dbContext = (AppDbContext)contextResolver.GetContext();
            CurrentUserRepository = currentUserRepository;
            Logger = loggerFactory.CreateLogger<TEntity>();
            ConstraintProviders = constraintProviders;
            Options.Converters.Add(new ResourceObjectConverter(resourceGraph));
            Options.Converters.Add(new WriteOnlyRelationshipObjectConverter());
            Options.Converters.Add(new SingleOrManyDataConverterFactory());
            ResourceDefinitionAccessor = resourceDefinitionAccessor;
            ResourceGraph = resourceGraph;
        }
        public bool PublishAsSharedResource(string publishTo)
        {
            return publishTo.Contains("Internalization");
        }
        public bool PublishToAkuo(string? publishTo)
        {
            return publishTo != null && (publishTo.Contains("Public") || publishTo.Contains("Beta") || publishTo.Contains("OBTHelps"));
        }

        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction BeginTransaction()
        {
            return dbContext.Database.BeginTransaction();
        }

        public User? CurrentUser {
            get { return CurrentUserRepository.GetCurrentUser(); }
        }

        #region MultipleData //orgdata, projdata
        protected string InitData(bool withBracket = true)
        {
            return "{\"data\":" + (withBracket ? "[" : "");
        }

        protected string FinishData(bool withBracket = true)
        {
            return (withBracket ? "]" : "") + "}";
        }

        protected bool CheckAdd(
            int check,
            string thisData,
            DateTime dtBail,
            ref int start,
            ref string data
        )
        {
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return false;
            if (start == check)
            {
                if (data.Length + thisData.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisData;
                start++;
            }
            return true;
        }
        protected static string ToJson<TResource>(IEnumerable<TResource> resources, ResourceInfo resourceInfo)
             where TResource : class, IIdentifiable
        {
            string? withIncludes =
            SerializerHelpers.ResourceListToJson(
                resources,
                resourceInfo.ResourceGraph,
                resourceInfo.Options,
                resourceInfo.ResourceDefinitionAccessor,
                resourceInfo.MetaBuilder
            );
            if (withIncludes.Contains("included"))
            {
                dynamic tmp = JObject.Parse(withIncludes);
                tmp.Remove("included");
                withIncludes = tmp.ToString();
            }
            //will this take it out of transcriptions also?? Seems to be ok!!
            Regex rgxnewlines = NewLineRegex();
            Regex rgxmultiplespaces = TabSpaceRegex();
            withIncludes = rgxnewlines.Replace(withIncludes, "");
            return rgxmultiplespaces.Replace(withIncludes, " ");
        }
        protected bool CheckAddData<TResource>(
               int check,
               IQueryable<TResource> input,
               DateTime dtBail,
               ref int start,
               ref string data,
               ResourceInfo resourceInfo
            ) where TResource : BaseModel
        {
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return false;
            int startId = -1;
            int starttable = StartIndex.GetStart(start, ref startId);
            int lastId = -1;
            if (starttable == check)
            {
                List<TResource>? lst = startId > 0 ? [.. input.Where(m => m.Id >= startId).OrderBy(x => x.Id)] : [.. input.OrderBy(x => x.Id)];
                string thisData = ToJson(lst, resourceInfo);
                while (thisData.Length > (1000000 * 4))
                {
                    int cnt = lst.Count;
                    TResource mid = lst[cnt/2];
                    lastId = mid.Id;
                    lst = [.. input.Where(m => m.Id >= startId && m.Id < lastId).OrderBy(x => x.Id)];
                    thisData = ToJson(lst, resourceInfo);
                }
                if (data.Length + thisData.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisData;
                start = StartIndex.SetStart(starttable, ref lastId);
                return lastId == 0;
            }
            return true;
        }
        #endregion

        //public version of GetAll which avoids current user checks
        public IQueryable<TEntity> Get()
        {
            return GetAll();
        }

        #region filters
        public IQueryable<TEntity> FromIdList(IQueryable<TEntity> entities, string idList)
        {
            string[] ids = idList.Replace("'", "").Split("|");
            return entities.Where(e => ids.Any(i => i == e.Id.ToString()));
        }

        public abstract IQueryable<TEntity> FromCurrentUser(IQueryable<TEntity>? entities); //force this one
        public abstract IQueryable<TEntity> FromProjectList(
            IQueryable<TEntity>? entities,
            string idList
        );

        protected virtual IQueryable<TEntity> FromStartIndex(
            IQueryable<TEntity>? entities,
            string startIndex,
            string version = "",
            string projectid = ""
        )
        {
            return entities ?? Enumerable.Empty<TEntity>().AsQueryable();
        }

        protected virtual IQueryable<TEntity> FromPlan(QueryLayer layer, string planId)
        {
            return base.ApplyQueryLayer(layer);
        }

        protected override IQueryable<TEntity> ApplyQueryLayer(QueryLayer layer)
        {
            ExpressionInScope[] expressions = [.. ConstraintProviders
                .SelectMany(provider => provider.GetConstraints())
                .Where(expressionInScope => expressionInScope.Scope == null)];

            if (layer.Filter?.Has(FilterConstants.ID) ?? false) //internal call after insert...if external, we caught it in GetAsync
                return base.ApplyQueryLayer(layer);

            foreach (ExpressionInScope ex in expressions)
            {
                /* multiple filters on the request will be or'd together */
                /* note this is not an all purpose solution, but one very specific to our case
                 * we only use multiples on orgdata,projdata */
                if (ex.Expression.GetType().IsAssignableFrom(typeof(LogicalExpression)))
                {
                    LogicalExpression logex = (LogicalExpression)ex.Expression;
                    IReadOnlyCollection<FilterExpression> terms = logex.Terms;
                    string projectid = "",
                        startid = "",
                        version = "";
                    foreach (FilterExpression term in terms)
                    {
                        if (term.Field() == FilterConstants.PROJECT_SEARCH_TERM)
                            projectid = term.Value().Replace("'", "");
                        else if (term.Field() == FilterConstants.DATA_START_INDEX)
                            startid = term.Value().Replace("'", "");
                        else if (term.Field() == FilterConstants.JSONFILTER)
                            version = term.Value().Replace("'", "");
                    }
                    if (startid != "")
                    {
                        layer.Filter = null;
                        return FromStartIndex(
                            base.ApplyQueryLayer(layer),
                            startid,
                            version,
                            projectid
                        );
                    }
                }
                else
                    switch (ex.Field())
                    {
                        case FilterConstants.IDLIST:
                            layer.Filter = null;
                            return FromIdList(base.ApplyQueryLayer(layer), ex.Value());
                        case FilterConstants.PROJECT_LIST:
                            layer.Filter = null;
                            return FromProjectList(base.ApplyQueryLayer(layer), ex.Value());
                    }
                ;
            }
            return FromCurrentUser(base.ApplyQueryLayer(layer));
        }

        [GeneratedRegex("\r\n|\n")]
        private static partial Regex NewLineRegex();
        [GeneratedRegex("\t|\\s+")]
        private static partial Regex TabSpaceRegex();
        #endregion
    }
}
