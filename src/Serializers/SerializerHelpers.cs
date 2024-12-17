using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Serialization.Response;
using System.Collections.Immutable;
using System.Text.Json;

namespace SIL.Transcriber.Serialization
{
    public static class SerializerHelpers
    {
        public static IImmutableSet<IncludeElementExpression> GetSingleIncludes(
            ResourceType resourceType,
            IImmutableSet<IncludeElementExpression>? existingIncludes = null
        )
        {
            IReadOnlyCollection<RelationshipAttribute>? rcol = resourceType.Relationships;
            existingIncludes ??= ImmutableHashSet<IncludeElementExpression>.Empty;
            List<IncludeElementExpression> allIncludes = new(existingIncludes);

            foreach (RelationshipAttribute r in rcol)
            {
                if (
                    !allIncludes.Any(include => include.Relationship.Property.Name == r.PublicName)
                    && r is HasOneAttribute
                )
                {
                    allIncludes.Add(new IncludeElementExpression(r));
                }
            }
            return allIncludes.ToImmutableHashSet();
        }

        private static ResponseModelAdapter GetAdapter<TResource>(
            bool isCollection,
            IResourceGraph resourceGraph,
            IJsonApiOptions options,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            IMetaBuilder metaBuilder
        ) where TResource : class, IIdentifiable
        {
            ResourceType resourceType = resourceGraph.GetResourceType<TResource>();

            IncludeExpression includeExpression = new(GetSingleIncludes(resourceType));

            JsonApiRequest request =
                new()
                {
                    Kind = EndpointKind.Primary,
                    PrimaryResourceType = resourceType,
                    IsCollection = isCollection,
                    IsReadOnly = true
                };

            HiddenLinksBuilder? linksBuilder = new();
            IncludeCache includeCache = new(includeExpression);
            EveryFieldCache sparseFieldSetCache = new();
            EmptyQueryStringAccessor queryStringAccessor = new();

            return new(
                request,
                options,
                linksBuilder,
                metaBuilder,
                resourceDefinitionAccessor,
                includeCache,
                sparseFieldSetCache,
                queryStringAccessor
            );
        }

        public static string ResourceListToJson<TResource>(
            IEnumerable<TResource> resources,
            IResourceGraph resourceGraph,
            IJsonApiOptions options,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            IMetaBuilder metaBuilder
        ) where TResource : class, IIdentifiable
        {
            ResponseModelAdapter adapter = GetAdapter<TResource>(
                true,
                resourceGraph,
                options,
                resourceDefinitionAccessor,
                metaBuilder
            );
            Document document = adapter.Convert(resources);
            return JsonSerializer.Serialize(document, options.SerializerOptions);
        }

        public static string ResourceToJson<TResource>(
            TResource resource,
            IResourceGraph resourceGraph,
            IJsonApiOptions options,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            IMetaBuilder metaBuilder
        ) where TResource : class, IIdentifiable
        {
            ResponseModelAdapter adapter = GetAdapter<TResource>(
                false,
                resourceGraph,
                options,
                resourceDefinitionAccessor,
                metaBuilder
            );
            Document document = adapter.Convert(resource);
            return JsonSerializer.Serialize(document, options.SerializerOptions);
        }

        /// <summary>
        /// Enables to explicitly set the inclusion chain to use, which is normally based on the '?include=' query string parameter.
        /// </summary>
        private sealed class IncludeCache(IncludeExpression? include) : IEvaluatedIncludeCache
        {
            private IncludeExpression? _include = include;

            public void Set(IncludeExpression include)
            {
                _include = include;
            }

            public IncludeExpression? Get()
            {
                return _include;
            }
        }

        /// <summary>
        /// Hides all JSON:API links.
        /// </summary>
        private sealed class HiddenLinksBuilder : ILinkBuilder
        {
            public TopLevelLinks? GetTopLevelLinks()
            {
                return null;
            }

            public ResourceLinks? GetResourceLinks(
                ResourceType resourceType,
                IIdentifiable resource
            )
            {
                return null;
            }

            public RelationshipLinks? GetRelationshipLinks(
                RelationshipAttribute relationship,
                IIdentifiable leftResource
            )
            {
                return null;
            }
        }

        /// <summary>
        /// Forces to return all fields (attributes and relationships), which is normally based on the '?fields=' query string parameter.
        /// </summary>
        private sealed class EveryFieldCache : ISparseFieldSetCache
        {
            public IImmutableSet<ResourceFieldAttribute> GetSparseFieldSetForQuery(
                ResourceType resourceType
            )
            {
                return resourceType.Fields.ToImmutableHashSet();
            }

            public IImmutableSet<AttrAttribute> GetIdAttributeSetForRelationshipQuery(
                ResourceType resourceType
            )
            {
                return resourceType.Attributes.ToImmutableHashSet();
            }

            public IImmutableSet<ResourceFieldAttribute> GetSparseFieldSetForSerializer(
                ResourceType resourceType
            )
            {
                return resourceType.Fields.ToImmutableHashSet();
            }

            public void Reset() { }
        }

        /// <summary>
        /// Ignores any incoming query string parameters.
        /// </summary>
        private sealed class EmptyQueryStringAccessor : IRequestQueryStringAccessor
        {
            public IQueryCollection Query { get; } = new QueryCollection();
        }
    }
}
