using Amazon.Auth.AccessControlPolicy;
using Humanizer.Localisation;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;

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
    public override async Task OnWritingAsync(
    Section resource,
    WriteOperationKind writeOperation,
    CancellationToken cancellationToken
    )
    {
        if (resource.Published && AppDbContext.Sections.Any(s => s.Id == resource.Id && !s.Published))
        {
            PublishSection(resource);
        }
        _ = MakeMediafilePublicAsync(writeOperation, MediafileService, resource.TitleMediafileId);
        await base.OnWritingAsync(resource, writeOperation, cancellationToken);
    }

    private void PublishSection(Section section)
    {
        string fp = HttpContext != null ? HttpContext.GetFP() ?? "" : "";
        HttpContext?.SetFP("publish");
        PublishPassages(section.Id);
        AppDbContext.SaveChanges();
        HttpContext?.SetFP(fp);
    }
    private void PublishPassages(int sectionid) {
        List<Passage> passages = AppDbContext.Passages.Where(p => p.SectionId == sectionid).ToList();
        foreach (Passage? passage in passages)
        {
            Mediafile? mediafile = AppDbContext.Mediafiles
                        .Where(m => m.PassageId == passage.Id && m.ArtifactTypeId == null && !m.Archived)
                        .OrderByDescending(m => m.VersionNumber)
                        .FirstOrDefault();
            if (mediafile != null)
            {
                mediafile.ReadyToShare = true;
                AppDbContext.Mediafiles.Update(mediafile);
                _ = MakeMediafilePublicAsync(WriteOperationKind.UpdateResource, MediafileService, mediafile.Id);
            }
        }
    }
}
