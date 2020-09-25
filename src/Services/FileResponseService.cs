using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class FileResponseService : EntityResourceService<FileResponse>
    {
             public FileResponseService(
                IJsonApiContext jsonApiContext,
                FileResponseRepository repository,
                ILoggerFactory loggerFactory
            ) : base(jsonApiContext,repository, loggerFactory)
            {
            }
        }
}
