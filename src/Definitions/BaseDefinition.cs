using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.Extensions.Primitives;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Collections.Immutable;


namespace SIL.Transcriber.Definitions
{
    public class BaseDefinition<TEntity> : JsonApiResourceDefinition<TEntity, int> where TEntity : BaseModel
    {
        protected ILogger<TEntity> Logger { get; set; }

        public BaseDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory)
        : base(resourceGraph)
        {
            Logger = loggerFactory.CreateLogger<TEntity>();
        }
        public bool Has(StringValues parameterValue, string param)
        {
            return parameterValue.Contains(param);
        }

        public override IImmutableSet<IncludeElementExpression> OnApplyIncludes(IImmutableSet<IncludeElementExpression> existingIncludes)
        {
            ResourceType? rt = ResourceGraph.GetResourceType<TEntity>();
            IReadOnlyCollection<RelationshipAttribute>? rcol = rt.Relationships;
            List<IncludeElementExpression> allIncludes = new(existingIncludes);

            foreach (RelationshipAttribute r in rcol)
            {
                if (!allIncludes.Any(include => include.Relationship.Property.Name == r.PublicName) &&
                    r is HasOneAttribute)
                {
                    allIncludes.Add(new IncludeElementExpression(r));
                }
            }
            allIncludes.ForEach(i => Logger.LogInformation("xx {0}", i.ToString()));

            return allIncludes.ToImmutableHashSet();
        }
        public override FilterExpression? OnApplyFilter(FilterExpression? existingFilter)
        {
            if (existingFilter != null && existingFilter.Has(FilterConstants.DATA_START_INDEX))
                return null;
            return base.OnApplyFilter(existingFilter);
        }
    }
}
