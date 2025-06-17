using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class SectionPassageService(
        IHttpContextAccessor httpContextAccessor,
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Sectionpassage> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        SectionPassageRepository myRepository,
        AppDbContextResolver contextResolver
        ) : JsonApiResourceService<Sectionpassage, int>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor
            )
    {
        protected SectionPassageRepository MyRepository { get; } = myRepository;
        protected readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;

        //protected IJsonApiOptions options { get; }
        protected ILogger<Sectionpassage> Logger { get; set; } = loggerFactory.CreateLogger<Sectionpassage>();
        protected IResourceChangeTracker<Sectionpassage> ResourceChangeTracker = resourceChangeTracker;

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member.
        public override async Task<Sectionpassage?> GetAsync(int id, CancellationToken cancelled)
#pragma warning restore CS8609 // Nullability of reference types in return type doesn't match overridden member.
        {
            Sectionpassage? entity = await base.GetAsync(id, cancelled); // dbContext.Sectionpassages.Where(e => e.Id == id).FirstOrDefault();
            return (entity?.Complete ?? false) ? entity : null;

        }

        public override async Task<Sectionpassage?> CreateAsync(
            Sectionpassage entity,
            CancellationToken cancellationToken
        )
        {
            object? input = entity.Data != null ? JsonConvert.DeserializeObject(entity.Data) : null;

            if (input == null || !input.GetType().IsAssignableFrom(typeof(JArray)))
                throw new Exception("Invalid input");

            JArray data = (JArray)input;

            if (data.Count == 0)
                return entity;

            Sectionpassage? inprogress = MyRepository.GetByUUID(entity.Uuid);
            if (inprogress != null)
            {
                if (inprogress.Complete)
                {
                    /* another call completed successfully, so call off future retries */
                    return entity;
                }
                else
                {
                    /* another call is in progress...but in case it fails and we need a retry, fail this one */
                    return null; // throw new JsonApiException(new Error(502,"orbit is dumb"));
                }
            }
            entity.DateCreated = DateTime.UtcNow;
            try
            {
                Sectionpassage? newentity = await base.CreateAsync(entity, new CancellationToken());
                if (newentity == null)
                    return null;
                entity = newentity;
            }
            catch (Exception ex)
            {
                Logger.LogError("{ex}", ex);
                if (ex.InnerException != null && ex.InnerException.Message.Contains("23505"))
                    return null;
            }
            using IDbContextTransaction transaction = MyRepository.BeginTransaction();
            HttpContext?.SetFP("onlinesave");
            try
            {
                IEnumerable<JToken> updsecs = data.Where(
                    d => ((bool?)d[0]?["issection"] ?? false) && ((bool?)d[0]?["changed"] ?? false)
                );

                //add all sections
                List<Section> updsections = [];

                foreach (JArray item in updsecs.Cast<JArray>())
                {
                    updsections.Add(
                        (item[0]?["id"] ?? "").ToString() != ""
                            ? MyRepository.GetSection((int?)item[0]["id"] ?? 0).UpdateFrom(item[0])
                            : new Section().UpdateFrom(item[0], entity.PlanId)
                    );
                }
                if (updsections.Count > 0)
                {
                    await MyRepository.BulkUpdateSections(updsections);
                    int ix = 0;
                    foreach (JArray item in updsecs)
                    {
                        item[0]["id"] = updsections[ix].Id;
                        ix++;
                    }
                }
                int lastSectionId = 0;
                /* process all the passages now */
                List<JToken> updpass = [];
                List<Passage> updpassages = [];
                List<Passage> delpassages = [];
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (JArray item in data)
                {
                    if ((bool)item[0]["issection"])
                    {
                        if (item[0]["id"] != null && item[0]["id"].ToString() != "") //saving in chunks may not have saved this section...passages will be marked unchanged
                        {
                            lastSectionId = (int)item[0]["id"];
                            if (item.Count > 1)
                            {
                                if ((bool)item[1]["changed"])
                                {
                                    updpass.Add(item);
                                    updpassages.Add(
                                        item[1]["id"] != null && item[1]["id"].ToString() != ""
                                            ? MyRepository
                                                .GetPassage((int)item[1]["id"])
                                                .UpdateFrom(item[1], lastSectionId)
                                            : new Passage().UpdateFrom(item[1], lastSectionId)
                                    );
                                }
                                else if (item[1]["deleted"] != null && (bool)item[1]["deleted"])
                                {
                                    delpassages.Add(
                                        MyRepository
                                            .GetPassage((int)item[1]["id"])
                                            .UpdateFrom(item[1])
                                    );
                                }
                            }
                        }
                    }
                    else if ((bool?)item[0]["changed"] ?? false)
                    {
                        updpass.Add(item);
                        updpassages.Add(
                            (item[0]?["id"]?.ToString() ?? "") != ""
                                ? MyRepository.GetPassage((int)item[0]["id"]).UpdateFrom(item[0], lastSectionId)
                                : new Passage().UpdateFrom(item[0], lastSectionId)
                        );
                    }
                    else if (item[0]["deleted"] != null && ((bool?)item[0]["deleted"] ?? false))
                    {
                        delpassages.Add(
                            MyRepository.GetPassage((int?)item[0]["id"] ?? 0).UpdateFrom(item[0])
                        );
                    }
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
                if (updpassages.Count > 0)
                {
                    //Logger.LogInformation($"updpassages {updpassages.Count} {updpassages}");
                    _ = MyRepository.BulkUpdatePassages(updpassages);
                    int ix = 0;
                    foreach (JArray item in updpass)
                    {
                        item[item.Count - 1]["id"] = updpassages[ix].Id;
                        _ = MyRepository.UpdateSectionModified(updpassages[ix].SectionId);
                        ix++;
                    }
                }
                if (delpassages.Count > 0)
                {
                    _ = MyRepository.BulkDeletePassages(delpassages);
                    delpassages.ForEach(p => MyRepository.UpdateSectionModified(p.SectionId));
                }
                IEnumerable<JToken> delsecs = data.Where(
                    d => ((bool?)d[0]?["issection"] ?? false) && ((bool?)d[0]?["deleted"] ?? false)
                );
                List<Section> delsections = [];
                foreach (JArray item in delsecs)
                {
                    delsections.Add(MyRepository.GetSection((int?)item[0]["id"] ?? 0));
                }
                _ = MyRepository.BulkDeleteSections(delsections);
                _ = MyRepository.UpdatePlanModified(entity.PlanId);
                transaction.Commit();
                entity.Data = JsonConvert.SerializeObject(data);
                entity.Complete = true;
                //this doesnt work  _ = await UpdateAsync(entity.Id, entity, new CancellationToken());
                dbContext.Sectionpassages.Update(entity);
                dbContext.SaveChanges();
                return entity;
            }
            catch (Exception ex)
            {
                Logger.LogCritical("Insert Error {ex}", ex);
                /* I'm giving up...let the next one try */
                transaction.Rollback();
                await MyRepository.DeleteAsync(entity, entity.Id, new CancellationToken());
                throw new JsonApiException(
                    new ErrorObject(System.Net.HttpStatusCode.InternalServerError),
                    new Exception(ex.Message)
                );
            }
        }
    }
}
