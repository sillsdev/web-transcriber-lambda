using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class GraphicService : BaseArchiveService<Graphic>
    {
        private readonly IS3Service S3service;
        readonly private HttpContext? HttpContext;
        public GraphicService(
            IHttpContextAccessor httpContextAccessor,
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
        { 
            HttpContext = httpContextAccessor.HttpContext; 
            S3service = s3service; 
        }
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
                    HttpContext?.SetFP("graphicimage");
                    _ = await base.UpdateArchivedAsync(newEntity.Id, newEntity, cancellationToken);
                    return newEntity;
                }
            }
            entity.Info = await SaveImages(JObject.Parse(entity.Info ?? "{}"));
            HttpContext?.SetFP("graphicimage");
            return await base.CreateAsync(entity, cancellationToken);
        }

        public override async Task<Graphic?> UpdateAsync(int id, Graphic entity, CancellationToken cancellationToken)
        {
            entity.Info = await SaveImages(JObject.Parse(entity.Info ?? "{}"));
            HttpContext?.SetFP("graphicimage");
            return await base.UpdateAsync(id, entity, cancellationToken);
        }
    }
}
