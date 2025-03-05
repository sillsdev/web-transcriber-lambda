using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Definitions;

public class ArtifactCategoryDefinition(
    IResourceGraph resourceGraph,
    ILoggerFactory loggerFactory,
    IJsonApiRequest Request,
        MediafileService mediafileService
    ) : BaseDefinition<Artifactcategory>(resourceGraph, loggerFactory, Request)
{
    private readonly MediafileService MediafileService = mediafileService;

    public override async Task OnWritingAsync(
    Artifactcategory resource,
    WriteOperationKind writeOperation,
    CancellationToken cancellationToken
    )
    {
        _ = PublishMediafile(writeOperation, MediafileService, PublishTitle, resource.TitleMediafileId);
        await base.OnWritingAsync(resource, writeOperation, cancellationToken);
    }
}