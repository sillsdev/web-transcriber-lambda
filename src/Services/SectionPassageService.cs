using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
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

        public override async Task<Sectionpassage> GetAsync(int id, CancellationToken cancelled)
        {
            Sectionpassage entity = await base.GetAsync(id, cancelled); // dbContext.Sectionpassages.Where(e => e.Id == id).FirstOrDefault();

            // If the create operation is still in progress, return the entity so the caller can poll it.
            // else attempt to continue processing so the record can finish.
            return !entity.Complete ? await ProcessData(entity) : entity;
        }

        private async Task<Sectionpassage> ProcessData(Sectionpassage entity)
        {
            object? input = entity.Data != null ? JsonConvert.DeserializeObject(entity.Data) : null;

            if (input == null || !input.GetType().IsAssignableFrom(typeof(JArray)))
                throw new Exception("Invalid input");

            JArray data = (JArray)input;
            int? TokToInt(JToken? t)
            {
                try
                {
                    if (t == null)
                        return null;
                    string s = t.ToString();
                    if (string.IsNullOrWhiteSpace(s))
                        return null;
                    if (int.TryParse(s, out int v))
                        return v;
                }
                catch { }
                return null;
            }
            bool TokToBool(JToken? t)
            {
                try
                {
                    if (t == null)
                        return false;
                    string s = t.ToString();
                    if (string.IsNullOrWhiteSpace(s))
                        return false;
                    if (bool.TryParse(s, out bool v))
                        return v;
                }
                catch { }
                return false;
            }
            using IDbContextTransaction transaction = MyRepository.BeginTransaction();
            HttpContext?.SetFP("onlinesave");
            // Use the dtBail pattern used elsewhere in the codebase: bail after a fixed wall-clock time
            DateTime dtBail = DateTime.Now.AddSeconds(10);
            int loopCount = 0;

            // local helper to persist partial progress and exit when dtBail is exceeded
            async Task<bool> BailIfNeeded()
            {
                if (DateTime.Now > dtBail)
                {
                    entity.Data = JsonConvert.SerializeObject(data);
                    // release processing claim so another worker can pick up
                    // Perform a direct SQL update to avoid EF tracking conflicts when saving partial progress
                    try
                    {
                        await dbContext.Sectionpassages
                            .Where(x => x.Id == entity.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.Data, entity.Data)
                            .SetProperty(x => x.Processing, false)
                            .SetProperty(x => x.Complete, false)
                        );
                    }
                    catch
                    {
                        // fallback to tracked update if raw SQL fails
                        try
                        {
                            entity.Processing = false;
                            entity.ProcessingStarted = null;
                            entity.Complete = false;
                            dbContext.Sectionpassages.Update(entity);
                            dbContext.SaveChanges();
                        }
                        catch { }
                    }

                    transaction.Commit();
                    return true;
                }
                return false;
            }

            try
            {
                // Sections that need updating/creating. If a previous run already completed the
                // section work it will set a `complete` flag on the section object so we skip it.
                IEnumerable<JToken> updsecs = data.Where(
                    d => TokToBool(d[0]?["issection"]) && TokToBool(d[0]?["changed"]) && !TokToBool(d[0]?["complete"])
                );

                //add all sections
                List<Section> updsections = [];

                foreach (JArray item in updsecs.Cast<JArray>())
                {
                    int? sid = TokToInt(item[0]?["id"]);
                    updsections.Add(
                        sid.HasValue
                            ? MyRepository.GetSection(sid.Value).UpdateFrom(item[0])
                            : new Section().UpdateFrom(item[0], entity.PlanId)
                    );
                    if (DateTime.Now > dtBail)
                        break;
                }
                if (updsections.Count > 0)
                {
                    await MyRepository.BulkUpdateSections(updsections);
                    int ix = 0;
                    foreach (JArray item in updsecs)
                    {
                        item[0]["id"] = updsections[ix].Id;
                        // mark this section as completed so future resumes do not re-run section updates
                        item[0]["complete"] = true;
                        ix++;
                    }
                }
                // Bail check before starting heavy DB work
                if (await BailIfNeeded())
                    return entity;
                int lastSectionId = 0;
                /* process all the passages now */
                List<JArray> updpass = [];
                List<Passage> updpassages = [];
                List<Passage> delpassages = [];

                foreach (JArray item in data)
                {
                    loopCount++;
                    if (DateTime.Now > dtBail)
                        break;
                    if (TokToBool(item[0]?["issection"]))
                    {
                        int? s = TokToInt(item[0]?["id"]);
                        if (s.HasValue) //saving in chunks may not have saved this section...passages will be marked unchanged
                        {
                            lastSectionId = s.Value;
                            if (item.Count > 1)
                            {
                                int? pid = TokToInt(item[1]?["id"]);
                                if (TokToBool(item[1]?["changed"]) && !TokToBool(item[1]?["complete"]))
                                {
                                    updpass.Add(item);
                                    updpassages.Add(
                                        pid.HasValue
                                            ? MyRepository
                                                .GetPassage(pid.Value)
                                                .UpdateFrom(item[1], lastSectionId)
                                            : new Passage().UpdateFrom(item[1], lastSectionId)
                                    );
                                    item[1]["complete"] = true;
                                }
                                else if (TokToBool(item[1]?["deleted"]) && !TokToBool(item[1]?["complete"]) && pid.HasValue)
                                {
                                    delpassages.Add(
                                        MyRepository
                                            .GetPassage(pid.Value)
                                    );
                                    item[1]["complete"] = true;
                                }
                            }
                        }
                    }
                    // process any top-level passages that are not under a section
                    else
                    {
                        int? pid = TokToInt(item[0]?["id"]);
                        if (TokToBool(item[0]?["changed"]) && !TokToBool(item[0]?["complete"]))
                        {
                            updpass.Add(item);
                            updpassages.Add(
                                pid.HasValue
                                    ? MyRepository.GetPassage(pid.Value).UpdateFrom(item[0], lastSectionId)
                                    : new Passage().UpdateFrom(item[0], lastSectionId)
                            );
                            item[0]["complete"] = true;
                        }
                        else if (TokToBool(item[0]?["deleted"]) && !TokToBool(item[0]?["complete"]))
                        {
                            delpassages.Add(
                                MyRepository.GetPassage((int?)item[0]["id"] ?? 0).UpdateFrom(item[0])
                            );
                            item[0]["complete"] = true;
                        }
                    }
                }

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

                if (await BailIfNeeded())
                    return entity;
                IEnumerable<JToken> delsecs = data.Where(
                    d => TokToBool(d[0]?["issection"]) && TokToBool(d[0]?["deleted"]) && !TokToBool(d[0]?["complete"])
                );
                List<Section> delsections = [];
                foreach (JArray item in delsecs)
                {
                    delsections.Add(MyRepository.GetSection((int?)item[0]["id"] ?? 0));
                    item[0]["complete"] = true;
                }
                if (delsections.Count > 0)
                {
                    _ = MyRepository.BulkDeleteSections(delsections);
                }

                _ = MyRepository.UpdatePlanModified(entity.PlanId);
                transaction.Commit();
                entity.Data = JsonConvert.SerializeObject(data);
                entity.Complete = true;
                // finished processing, clear the processing claim
                await dbContext.Sectionpassages
                    .Where(x => x.Id == entity.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Data, entity.Data)
                    .SetProperty(x => x.Processing, false)
                    .SetProperty(x => x.Complete, true)
                );
                return entity;
            }
            catch (Exception ex)
            {
                Logger.LogCritical("Insert Error {ex}", ex);
                /* I'm giving up...let the next one try */
                try
                {
                    if (transaction.GetDbTransaction().Connection?.State == System.Data.ConnectionState.Open)
                        transaction.Rollback();
                }
                catch (Exception rollbackEx)
                {
                    Logger.LogError(rollbackEx, "Rollback failed.");
                }
                //await MyRepository.DeleteAsync(entity, entity.Id, new CancellationToken());
                throw new JsonApiException(
                    new ErrorObject(System.Net.HttpStatusCode.InternalServerError),
                    new Exception(ex.Message)
                );
            }
        }
        private async Task ClaimIt(Sectionpassage entity)
        {
            // not currently processing: claim it
            entity.Processing = true;
            entity.ProcessingStarted = DateTime.UtcNow;
            entity.Complete = false;
            if (entity.Id != 0)
            {
                await dbContext.Sectionpassages
                    .Where(x => x.Id == entity.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Processing, true)
                    .SetProperty(x => x.Complete, false)
                    .SetProperty(x => x.ProcessingStarted, entity.ProcessingStarted)
                );
            }
        }
        private async Task<bool> DidIClaimIt(Sectionpassage existing)
        {
            const int PROCESSING_STALE_SECONDS = 31;
            if (existing.Complete)
            {
                /* another call completed successfully, so return that record */
                return false;
            }

            /* existing incomplete record found - claim/resume it instead of inserting */
            if (existing.Processing)
            {
                if (existing.ProcessingStarted.HasValue && DateTime.UtcNow.Subtract(existing.ProcessingStarted.Value).TotalSeconds > PROCESSING_STALE_SECONDS)
                {
                    // steal stale claim
                    await ClaimIt(existing);
                    return true;
                }
                else
                {
                    // someone else is actively processing, return partial so caller can poll
                    return false;
                }
            }
            else
            {
                // not currently processing: claim it
                await ClaimIt(existing);
                return true;
            }
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

            Sectionpassage? existing = MyRepository.GetByUUID(entity.Uuid);

            if (existing != null)
            {
                if (await DidIClaimIt(existing))
                {
                    return await ProcessData(existing);
                }
                else
                    // it's done or someone else is actively processing, return partial so caller can poll
                    return existing;
            }
            else
            {
                entity.DateCreated = DateTime.UtcNow;
                try
                {
                    // claim-on-insert: mark processing before inserting so the first insert wins the claim
                    await ClaimIt(entity);

                    Sectionpassage? newentity = await base.CreateAsync(entity, new CancellationToken());
                    return newentity == null ? null : await ProcessData(newentity);
                }
                catch (Exception ex)
                {
                    Logger.LogError("{ex}", ex);
                    // duplicate UUID -> someone else inserted first. Load existing and apply claim/resume logic.
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("23505"))
                    {
                        existing = MyRepository.GetByUUID(entity.Uuid);
                        if (existing == null)
                            return null;
                        if (await DidIClaimIt(existing))
                        {
                            return await ProcessData(existing);
                        }
                        else
                            // it's done or someone else is actively processing, return partial so caller can poll
                            return existing;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
    }
}
