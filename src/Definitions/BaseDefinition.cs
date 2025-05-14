using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using Microsoft.Extensions.Primitives;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serializers;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Collections.Immutable;

namespace SIL.Transcriber.Definitions
{
    public class BaseDefinition<TEntity>(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request
        ) : JsonApiResourceDefinition<TEntity, int>(resourceGraph)
        where TEntity : BaseModel
    {
        protected ILogger<TEntity> Logger { get; set; } = loggerFactory.CreateLogger<TEntity>();
        protected string PublishTitle = "{\"Public\": \"true\"}";
        readonly private IJsonApiRequest Request = request;
        private bool TopLevel = true;

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
        public async Task<Mediafile?> PublishMediafile(WriteOperationKind writeOperation, MediafileService service, string publishTo, int? id)
        {
            return writeOperation != WriteOperationKind.DeleteResource &&
                writeOperation != WriteOperationKind.RemoveFromRelationship &&
                writeOperation != WriteOperationKind.AddToRelationship &&
                id != null
                ? await service.Publish((int)id, publishTo)
                : null;
        }
    }
}
