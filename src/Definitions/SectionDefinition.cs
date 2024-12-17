using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Definitions;

public class SectionDefinition(
    IResourceGraph resourceGraph,
    ILoggerFactory loggerFactory,
    IJsonApiRequest Request
    ) : BaseDefinition<Section>(resourceGraph, loggerFactory, Request)
{
        //Do not use a service here...the dbContext in the service isn't correct when called from definition
        //at least in onWritingAsync
}
