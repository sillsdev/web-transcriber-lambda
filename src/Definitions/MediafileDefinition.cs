using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Definitions
{
    public class MediafileDefinition(
        IResourceGraph resourceGraph,
        ILoggerFactory loggerFactory,
        IJsonApiRequest Request,
        AppDbContext appDbContext,
         MediafileService mediafileService
        ) : BaseDefinition<Mediafile>(resourceGraph, loggerFactory, Request)
    {
        private readonly AppDbContext AppDbContext = appDbContext;
        private readonly MediafileService MediafileService = mediafileService;

        public override async Task OnWritingAsync(
            Mediafile resource,
            WriteOperationKind writeOperation,
            CancellationToken cancellationToken
        )
        {
            if (writeOperation == WriteOperationKind.CreateResource)
            {
                resource.VersionNumber ??= 1;
                resource.Link ??= false;
                resource.Transcriptionstate ??= "transcribeReady";
                resource.PublishTo ??= "{}";
                if (resource.ResourcePassageId == null)
                {
                    if (resource.Passage != null)
                    {
                        Mediafile? mfs = AppDbContext.Mediafiles
                            .Where(mf => mf.PassageId == resource.Passage.Id && !mf.Archived)
                            .ToList()
                            .Where(mf => mf.ArtifactTypeId == resource.ArtifactTypeId) //mf.IsVernacular)
                            .OrderBy(m => m.VersionNumber)
                            .LastOrDefault();
                        if (mfs != null)
                        {
                            resource.VersionNumber = mfs.VersionNumber + 1;
                        }
                    }
                }
                else
                {
                    //pick the highest version media of the resource per passage
                    Mediafile? sourcemediafile = AppDbContext.Mediafiles
                        .Where(x =>
                                x.PassageId == resource.ResourcePassageId
                                && x.ReadyToShare
                                && !x.Archived
                        )
                        .OrderByDescending(m => m.VersionNumber)
                        .FirstOrDefault();
                    resource.AudioUrl = sourcemediafile?.AudioUrl;
                    resource.S3File = sourcemediafile?.S3File;
                }
                if (resource.Passage != null && resource.EafUrl != null)
                {
                    //create a passage state change with this info
                    Passagestatechange psc =
                        new()
                        {
                            PassageId = resource.Passage.Id,
                            State = "",
                            Comments = resource.EafUrl
                        };

                    _ = AppDbContext.Passagestatechanges.Add(psc);
                    resource.EafUrl = "";
                }
            }
            else if (resource.ReadyToShare)
            { //&& AppDbContext.Mediafiles.Any(s => s.Id == resource.Id && !s.ReadyToShare))
                Mediafile? newInfo = await PublishMediafile(writeOperation, MediafileService, resource.PublishTo ?? PublishTitle, resource.Id);
                if (newInfo != null)
                {
                    resource.PublishedAs = newInfo.PublishedAs;
                    resource.PublishTo = newInfo.PublishTo;
                }
            }
            await base.OnWritingAsync(resource, writeOperation, cancellationToken);
        }
    }
}
