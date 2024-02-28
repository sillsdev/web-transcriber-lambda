using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Definitions;

public class ArtifactCategoryDefinition : BaseDefinition<Artifactcategory>
{
    private readonly MediafileService MediafileService;

    public ArtifactCategoryDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request,
            MediafileService mediafileService
    ) : base(resourceGraph, loggerFactory, Request)
    {
        MediafileService = mediafileService;

        //Do not use a service here...the dbContext in the service isn't correct when called from definition
        //at least in onWritingAsync
    }
    public override async Task OnWritingAsync(
    Artifactcategory resource,
    WriteOperationKind writeOperation,
    CancellationToken cancellationToken
    )
    {
        _ = MakeMediafilePublicAsync(writeOperation, MediafileService, resource.TitleMediafileId);
        await base.OnWritingAsync(resource, writeOperation, cancellationToken);
    }
}