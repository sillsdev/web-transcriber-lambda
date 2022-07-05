using System.Collections.Immutable;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal;
using JsonApiDotNetCore.QueryStrings;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Serialization.Response;

namespace SIL.Transcriber.Serialization;

public class MyResponseModelAdapter : ResponseModelAdapter
{
    private static readonly CollectionConverter CollectionConverter = new();

    private readonly ISparseFieldSetCache _sparseFieldSetCache;

    // Ensures that at most one ResourceObject (and one tree node) is produced per resource instance.
    private readonly Dictionary<IIdentifiable, ResourceObjectTreeNode> _resourceToTreeNodeCache = new(IdentifiableComparer.Instance);

    public MyResponseModelAdapter(IJsonApiRequest request, IJsonApiOptions options, ILinkBuilder linkBuilder, IMetaBuilder metaBuilder,
        IResourceDefinitionAccessor resourceDefinitionAccessor, IEvaluatedIncludeCache evaluatedIncludeCache, ISparseFieldSetCache sparseFieldSetCache,
        IRequestQueryStringAccessor requestQueryStringAccessor): base(request, options, linkBuilder, metaBuilder,
        resourceDefinitionAccessor, evaluatedIncludeCache, sparseFieldSetCache,
        requestQueryStringAccessor)
    {
        _sparseFieldSetCache = sparseFieldSetCache;
    }
    //SJH make this public
    public void TraverseResource(IIdentifiable resource, ResourceType resourceType, EndpointKind kind, IImmutableSet<IncludeElementExpression> includeElements,
        ResourceObjectTreeNode parentTreeNode, RelationshipAttribute? parentRelationship)
    {
        ResourceObjectTreeNode treeNode = GetOrCreateTreeNode(resource, resourceType, kind);

        if (parentRelationship != null)
        {
            parentTreeNode.AttachRelationshipChild(parentRelationship, treeNode);
        }
        else
        {
            parentTreeNode.AttachDirectChild(treeNode);
        }

        if (kind != EndpointKind.Relationship)
        {
            TraverseRelationships(resource, treeNode, includeElements, kind);
        }
    }

    private ResourceObjectTreeNode GetOrCreateTreeNode(IIdentifiable resource, ResourceType resourceType, EndpointKind kind)
    {
        if (!_resourceToTreeNodeCache.TryGetValue(resource, out ResourceObjectTreeNode? treeNode))
        {
            // In case of resource inheritance, prefer the derived resource type over the base type.
            ResourceType effectiveResourceType = GetEffectiveResourceType(resource, resourceType);

            ResourceObject resourceObject = ConvertResource(resource, effectiveResourceType, kind);
            treeNode = new ResourceObjectTreeNode(resource, effectiveResourceType, resourceObject);

            _resourceToTreeNodeCache.Add(resource, treeNode);
        }

        return treeNode;
    }
    public void PopulateRelationshipsInTree(ResourceObjectTreeNode rootNode, EndpointKind kind)
    {
        if (kind != EndpointKind.Relationship)
        {
            foreach (ResourceObjectTreeNode treeNode in rootNode.GetUniqueNodes())
            {
                PopulateRelationshipsInResourceObject(treeNode);
            }
        }
    }
    private void PopulateRelationshipsInResourceObject(ResourceObjectTreeNode treeNode)
    {
        IImmutableSet<ResourceFieldAttribute> fieldSet = _sparseFieldSetCache.GetSparseFieldSetForSerializer(treeNode.ResourceType);

        foreach (RelationshipAttribute relationship in treeNode.ResourceType.Relationships)
        {
            if (fieldSet.Contains(relationship))
            {
                PopulateRelationshipInResourceObject(treeNode, relationship);
            }
        }
    }
    private static void PopulateRelationshipInResourceObject(ResourceObjectTreeNode treeNode, RelationshipAttribute relationship)
    {
        SingleOrManyData<ResourceIdentifierObject> data = GetRelationshipData(treeNode, relationship);

        if (data.IsAssigned)
        {
            RelationshipObject relationshipObject = new ()
            {
                Links = null,
                Data = data
            };

            treeNode.ResourceObject.Relationships ??= new Dictionary<string, RelationshipObject?>();
            if (treeNode.ResourceObject.Relationships.ContainsKey(relationship.PublicName))
                Console.Write("here");
            else
                treeNode.ResourceObject.Relationships.Add(relationship.PublicName, relationshipObject);
        }
    }
    private static ResourceType GetEffectiveResourceType(IIdentifiable resource, ResourceType declaredType)
    {
        Type runtimeResourceType = resource.GetType();

        if (declaredType.ClrType == runtimeResourceType)
        {
            return declaredType;
        }

        ResourceType? derivedType = declaredType.GetAllConcreteDerivedTypes().FirstOrDefault(type => type.ClrType == runtimeResourceType);

        if (derivedType == null)
        {
            throw new InvalidConfigurationException($"Type '{runtimeResourceType}' does not exist in the resource graph.");
        }

        return derivedType;
    }

    private void TraverseRelationships(IIdentifiable leftResource, ResourceObjectTreeNode leftTreeNode, IImmutableSet<IncludeElementExpression> includeElements,
        EndpointKind kind)
    {
        foreach (IncludeElementExpression includeElement in includeElements)
        {
            TraverseRelationship(includeElement.Relationship, leftResource, leftTreeNode, includeElement, kind);
        }
    }

    private void TraverseRelationship(RelationshipAttribute relationship, IIdentifiable leftResource, ResourceObjectTreeNode leftTreeNode,
        IncludeElementExpression includeElement, EndpointKind kind)
    {
        if (!relationship.LeftType.ClrType.IsAssignableFrom(leftTreeNode.ResourceType.ClrType))
        {
            // Skipping over relationship that is declared on another derived type.
            return;
        }

        // In case of resource inheritance, prefer the relationship on derived type over the one on base type.
        RelationshipAttribute effectiveRelationship = !leftTreeNode.ResourceType.Equals(relationship.LeftType)
            ? leftTreeNode.ResourceType.GetRelationshipByPropertyName(relationship.Property.Name)
            : relationship;

        object? rightValue = effectiveRelationship.GetValue(leftResource);
        IReadOnlyCollection<IIdentifiable> rightResources = CollectionConverter.ExtractResources(rightValue);

        leftTreeNode.EnsureHasRelationship(effectiveRelationship);

        foreach (IIdentifiable rightResource in rightResources)
        {
            TraverseResource(rightResource, effectiveRelationship.RightType, kind, includeElement.Children, leftTreeNode, effectiveRelationship);
        }
    }
    private static SingleOrManyData<ResourceIdentifierObject> GetRelationshipData(ResourceObjectTreeNode treeNode, RelationshipAttribute relationship)
    {
        IReadOnlySet<ResourceObjectTreeNode>? rightNodes = treeNode.GetRightNodesInRelationship(relationship);

        if (rightNodes != null)
        {
            IEnumerable<ResourceIdentifierObject> resourceIdentifierObjects = rightNodes.Select(rightNode => new ResourceIdentifierObject
            {
                Type = rightNode.ResourceType.PublicName,
                Id = rightNode.ResourceObject.Id
            });

            return relationship is HasOneAttribute
                ? new SingleOrManyData<ResourceIdentifierObject>(resourceIdentifierObjects.SingleOrDefault())
                : new SingleOrManyData<ResourceIdentifierObject>(resourceIdentifierObjects);
        }

        return default;
    }

}
