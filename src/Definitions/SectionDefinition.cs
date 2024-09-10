using Amazon.Auth.AccessControlPolicy;
using Humanizer.Localisation;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;
using System.Collections.Generic;

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
        if (AppDbContext.Sections.Any(s => s.Id == resource.Id && s.Published != resource.Published))
        {
            PublishSection(resource, resource.Published);
        } 
        if (resource.Published || resource.Level < 3) //always do titles and movements
            _ = PublishMediafile(writeOperation, MediafileService, PublishTitle, resource.TitleMediafileId);
        await base.OnWritingAsync(resource, writeOperation, cancellationToken);
    }

    private void PublishSection(Section section, bool publish)
    {
        string fp = HttpContext != null ? HttpContext.GetFP() ?? "" : "";
        HttpContext?.SetFP("publish");
        PublishPassages(section.Id,section.PublishTo, publish);
        AppDbContext.SaveChanges();
        HttpContext?.SetFP(fp);
    }
    private void PublishPassages(int sectionid, string publishTo, bool publish) {
        List<Passage> passages = AppDbContext.Passages.Where(p => p.SectionId == sectionid).ToList();
        foreach (Passage? passage in passages)
        {
            IOrderedQueryable<Mediafile> mediafiles = AppDbContext.Mediafiles
                        .Where(m => m.PassageId == passage.Id && m.ArtifactTypeId == null && !m.Archived)
                        .OrderByDescending(m => m.VersionNumber);
#pragma warning disable CS8604 // Possible null reference argument.
            List < Mediafile > medialist = publish && mediafiles.Any()                 
                ? new List<Mediafile>
                {
                    mediafiles.FirstOrDefault()
                }
                : mediafiles.ToList();
            //if we are publishing, turn on shared notes.  If not publishing, leave them as they are
            if (publish && passage.SharedResourceId != null)
            {
                Sharedresource? note = AppDbContext.Sharedresources.Where(n => n.Id == passage.SharedResourceId).FirstOrDefault();
                if (note != null)
                {
                    int? notepsgid = note.PassageId;
                    Mediafile? notemediafile = AppDbContext.Mediafiles
                        .Where(m => m.PassageId == notepsgid && m.ArtifactTypeId == null && !m.Archived)
                        .OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                    if (notemediafile != null)
                        medialist.Add(notemediafile);
                }
            }
#pragma warning restore CS8604 // Possible null reference argument.

            medialist.ForEach(mediafile => {
                if (publish)
                    _ = PublishMediafile(WriteOperationKind.UpdateResource, MediafileService, publishTo, mediafile.Id);
                else
                {
                    mediafile.ReadyToShare = false;
                    AppDbContext.Mediafiles.Update(mediafile);
                }
            });
        }
    }
}
