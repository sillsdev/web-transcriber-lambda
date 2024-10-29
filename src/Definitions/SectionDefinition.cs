using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Definitions;

public class SectionDefinition : BaseDefinition<Section>
{
    private readonly AppDbContext AppDbContext;
    private readonly MediafileService MediafileService;
    readonly private HttpContext? HttpContext;
    public SectionDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request,
            AppDbContext appDbContext,
            MediafileService mediafileService,
             IHttpContextAccessor httpContextAccessor
    ) : base(resourceGraph, loggerFactory, Request) 
    {
        AppDbContext = appDbContext;
        MediafileService = mediafileService;
        HttpContext = httpContextAccessor.HttpContext;

        //Do not use a service here...the dbContext in the service isn't correct when called from definition
        //at least in onWritingAsync
    }
}
