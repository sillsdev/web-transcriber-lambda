using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Models;
using System.Collections.Immutable;

namespace SIL.Transcriber.Definitions
{
    public class UserDefinition : BaseDefinition<User>
    {
        public UserDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request
        ) : base(resourceGraph, loggerFactory, Request) { }

        public override IImmutableSet<IncludeElementExpression> OnApplyIncludes(
            IImmutableSet<IncludeElementExpression> existingIncludes
        )
        {
            return existingIncludes;
        }
    }
}
