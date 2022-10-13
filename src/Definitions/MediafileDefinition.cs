using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Definitions
{
    public class MediafileDefinition : BaseDefinition<Mediafile>
    {
        private readonly AppDbContext AppDbContext;

        public MediafileDefinition(
            IResourceGraph resourceGraph,
            ILoggerFactory loggerFactory,
            IJsonApiRequest Request,
            AppDbContext appDbContext
        ) : base(resourceGraph, loggerFactory, Request)
        {
            AppDbContext = appDbContext;

            //Do not use a service here...the dbContext in the service isn't correct when called from definition
            //at least in onWritingAsync
        }

        public override async Task OnWritingAsync(
            Mediafile resource,
            WriteOperationKind writeOperation,
            CancellationToken cancellationToken
        )
        {
            if (writeOperation == WriteOperationKind.CreateResource)
            {
                if (resource.VersionNumber == null)
                    resource.VersionNumber = 1;
                if (resource.Link == null)
                    resource.Link = false;
                if (resource.Transcriptionstate == null)
                    resource.Transcriptionstate = "transcribeReady";
                if (resource.ResourcePassageId == null)
                {
                    if (resource.IsVernacular && resource.Passage != null)
                    {
                        Mediafile? mfs = AppDbContext.Mediafiles
                            .Where(mf => mf.PassageId == resource.Passage.Id && !mf.Archived)
                            .ToList()
                            .Where(mf => mf.ArtifactTypeId is null) //mf.IsVernacular)
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
            await base.OnWritingAsync(resource, writeOperation, cancellationToken);
        }
    }
}
