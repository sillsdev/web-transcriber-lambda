using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries.Expressions;
using SIL.Transcriber.Models;
using System.Collections.Immutable;

namespace SIL.Transcriber.Definitions
{
    public class UserDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request
        ) : BaseDefinition<User>(resourceGraph, loggerFactory, Request)
    {
        public override IImmutableSet<IncludeElementExpression> OnApplyIncludes(
            IImmutableSet<IncludeElementExpression> existingIncludes
        )
        {
            //override base -- do not add all includes
            return existingIncludes;
        }
    }
}
