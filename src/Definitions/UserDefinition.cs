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
    public class UserDefinition : BaseDefinition<User>
    {
        public UserDefinition(IResourceGraph resourceGraph, ILoggerFactory loggerFactory) : base(resourceGraph, loggerFactory) { }

        public override IImmutableSet<IncludeElementExpression> OnApplyIncludes(IImmutableSet<IncludeElementExpression> existingIncludes)
        {
            ResourceType? rt = ResourceGraph.GetResourceType<User>();
            IReadOnlyCollection<RelationshipAttribute>? rcol = rt.Relationships;
            List<IncludeElementExpression> allIncludes = new(existingIncludes);

            foreach (RelationshipAttribute r in rcol)
            {
                if (!allIncludes.Any(include => include.Relationship.Property.Name == r.PublicName) && 
                    r is HasOneAttribute && r.PublicName != "last-modified-by-user")
                {
                    allIncludes.Add(new IncludeElementExpression(r));
                }
            }
            //allIncludes.ForEach(i => Logger.LogInformation("xx {0}", i.ToString()));
            return allIncludes.ToImmutableHashSet();
        }
    }
}

