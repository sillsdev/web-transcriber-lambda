using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class GraphicService : BaseArchiveService<Graphic>
    {
        private readonly IS3Service S3service;
        public GraphicService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Graphic> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            GraphicRepository repository,
            IS3Service s3service
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                repository
            )
        { S3service = s3service; }
        private async Task<string> SaveImages(JObject info)
        {
            string[] sizes = { "512", "1024" };
            foreach (string size in sizes)
            {
                JToken? graphic = info [size];
                if (graphic != null)
                {
                    string s = graphic["content"]?.ToString() ?? "";
                    string base64Data = s[(s.IndexOf(",") + 1)..].Trim();
                    try
                    {
                        using MemoryStream ms = new(Convert.FromBase64String(base64Data));
                        S3Response fileinfo = await S3service.UploadFileAsync(ms, true, graphic["type"]?.ToString() ?? "", graphic["name"]?.ToString() ?? "", "graphics");
                        graphic ["content"] = fileinfo.FileURL;
                        await S3service.MakePublic(fileinfo.Message, "graphics");
                        info [size] = graphic;
                    }
                    catch
                    {
                        //it's already converted by another linked passage, or it's crap
                    }
                }
            }
            return info.ToString();
        }

        public override async Task<Graphic?> CreateAsync(
                            Graphic entity,
                            CancellationToken cancellationToken)
        {
            if (entity.Organization != null)
            {
                Graphic? newEntity = Repo.Get()
                .Include(g => g.Organization)
                .Where(g => g.OrganizationId == entity.Organization.Id &&
                            g.ResourceId == entity.ResourceId && 
                            g.ResourceType == entity.ResourceType)
                .FirstOrDefault();

                if (newEntity != null)
                {
                    newEntity.Archived = false;
                    newEntity.Info = await SaveImages(JObject.Parse(entity.Info ?? "{}"));
                    _ = await base.UpdateArchivedAsync(newEntity.Id, newEntity, cancellationToken);
                    return newEntity;
                }
            }
            entity.Info = await SaveImages(JObject.Parse(entity.Info ?? "{}"));
            return await base.CreateAsync(entity, cancellationToken);
        }

    }
}
