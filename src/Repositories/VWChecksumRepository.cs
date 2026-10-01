using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Net;

namespace SIL.Transcriber.Repositories
{
    public class VWChecksumRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor
        ) : AppDbContextRepository<VWChecksum>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor
            )
    {
        private readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();
        private int? projectId;

        protected override IQueryable<VWChecksum> GetAll()
        {
            return projectId.HasValue
                ? dbContext.VWChecksums.FromSqlInterpolated($"SELECT * FROM public.get_vwchecksums({projectId.Value})")
                : base.GetAll();
        }

        protected override IQueryable<VWChecksum> ApplyQueryLayer(QueryLayer layer)
        {
            if (layer.Filter?.Has(FilterConstants.ID) ?? false)
                return base.ApplyQueryLayer(layer);

            projectId = TryGetProjectId(layer.Filter);
            if (!projectId.HasValue)
            {
                throw new JsonApiException(
                    new ErrorObject(HttpStatusCode.BadRequest),
                    new Exception("vwchecksums requires filter[project-id].")
                );
            }

            layer.Filter = null;
            return base.ApplyQueryLayer(layer);
        }

        private static int? TryGetProjectId(FilterExpression? filter)
        {
            if (filter is ComparisonExpression comparison)
            {
                string field = comparison.Left?.ToString() ?? "";
                string value = (comparison.Right?.ToString() ?? "").Replace("'", "");
                return field.Equals(FilterConstants.PROJECT_SEARCH_TERM, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(value, out int projectId)
                        ? projectId
                        : null;
            }

            if (filter is LogicalExpression logical)
            {
                foreach (FilterExpression term in logical.Terms)
                {
                    int? projectId = TryGetProjectId(term);
                    if (projectId.HasValue)
                        return projectId;
                }
            }

            return null;
        }
    }
}

