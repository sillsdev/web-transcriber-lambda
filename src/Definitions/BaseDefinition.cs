using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using Microsoft.Extensions.Primitives;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serialization;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Collections.Immutable;

namespace SIL.Transcriber.Definitions
{
    public class BaseDefinition<TEntity> : JsonApiResourceDefinition<TEntity, int>
        where TEntity : BaseModel
    {
        protected ILogger<TEntity> Logger { get; set; }
        readonly private IJsonApiRequest Request;
        private bool TopLevel = true;

        public BaseDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request
        ) : base(resourceGraph)
        {
            Logger = loggerFactory.CreateLogger<TEntity>();
            Request = request;
        }

        public bool Has(StringValues parameterValue, string param)
        {
            return parameterValue.Contains(param);
        }

        public override IImmutableSet<IncludeElementExpression> OnApplyIncludes(
            IImmutableSet<IncludeElementExpression> existingIncludes
        )
        {
            ResourceType rt = ResourceGraph.GetResourceType<TEntity>();
            if (
                TopLevel
                && (Request.IsReadOnly
                    || Request.WriteOperation == WriteOperationKind.CreateResource
                )
                && rt.PublicName == Request.PrimaryResourceType?.PublicName
            )
            {
                TopLevel = false;
                return SerializerHelpers.GetSingleIncludes(rt, existingIncludes);
            }
            return existingIncludes;
        }

        public override FilterExpression? OnApplyFilter(FilterExpression? existingFilter)
        {
            return existingFilter != null && existingFilter.Has(FilterConstants.DATA_START_INDEX) 
                ? null 
                : base.OnApplyFilter(existingFilter);
        }
    }
}
