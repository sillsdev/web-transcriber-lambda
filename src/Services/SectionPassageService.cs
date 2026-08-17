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
        AppDbContextResolver contextResolver,
        SectionRepository sectionRepository
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
        readonly private SectionRepository SectionRepository = sectionRepository;

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
            Logger.LogInformation("SPX ProcessData start: Sectionpassage.Id={Id}, PlanId={PlanId}", entity.Id, entity.PlanId);
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
            HttpContext?.SetFP("onlinesave");
            // Use the dtBail pattern used elsewhere in the codebase: bail after a fixed wall-clock time
            DateTime dtBail = DateTime.Now.AddSeconds(18);
            Logger.LogInformation("SPX ProcessData dtBail set to {dtBail:o}", dtBail);
            using IDbContextTransaction transaction = MyRepository.BeginTransaction();

            // local helper to persist partial progress and exit when dtBail is exceeded
            async Task<bool> BailIfNeeded()
            {
                if (DateTime.Now > dtBail)
                {
                    entity.Data = JsonConvert.SerializeObject(data);
                    entity.Processing = false;
                    entity.Complete = false;
                    // release processing claim so another worker can pick up
                    // Perform a direct SQL update to avoid EF tracking conflicts when saving partial progress
                    try
                    {
                        await UpdateIt(entity);
                    }
                    catch
                    {
                        // fallback to tracked update if raw SQL fails
                        try
                        {
                            dbContext.Sectionpassages.Update(entity);
                            dbContext.SaveChanges();
                        }
                        catch { }
                    }

                    Logger.LogInformation("SPX BailIfNeeded: bailing out for Sectionpassage.Id={Id} at {now}", entity.Id, DateTime.UtcNow);
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

                //add all sections in batches
                List<JArray> updsecItems = [.. updsecs.Cast<JArray>()];
                Logger.LogInformation("SPX Found {count} sections to add/update", updsecItems.Count);
                const int sectionBatchSize = 50;
                for (int si = 0; si < updsecItems.Count; si += sectionBatchSize)
                {
                    List<JArray> batchItems = [.. updsecItems.Skip(si).Take(sectionBatchSize)];
                    List<int> idsToFetch = [.. batchItems
                        .Select(item => TokToInt(item[0]?["id"]) ?? 0)
                        .Where(id => id != 0)
                        .Distinct()];

                    Dictionary<int, Section> existing = idsToFetch.Count > 0
                        ? dbContext.Sections
                            .Where(s => idsToFetch.Contains(s.Id))
                            .ToDictionary(s => s.Id)
                        : [];

                    List<Section> batchSections = [];
                    int batchIndex = (si / sectionBatchSize) + 1;
                    foreach (JArray item in batchItems)
                    {
                        int? sid = TokToInt(item[0]?["id"]);
                        Section? fromDb = sid.HasValue && existing.TryGetValue(sid.Value, out Section? existingSection)
                            ? existingSection
                            : null;
                        Section updatedSection = fromDb != null
                            ? fromDb.UpdateFrom(item[0])
                            : new Section().UpdateFrom(item[0], entity.PlanId);
                        batchSections.Add(updatedSection);
                        if (fromDb != null)
                            await SectionRepository.CheckPublish(updatedSection, fromDb);
                    }

                    if (batchSections.Count > 0)
                    {
                        await MyRepository.BulkUpdateSections(batchSections);
                        for (int j = 0; j < batchItems.Count; j++)
                        {
                            batchItems[j][0]["id"] = batchSections[j].Id;
                            // mark this section as completed so future resumes do not re-run section updates
                            batchItems[j][0]["complete"] = true;
                        }
                    }

                    if (await BailIfNeeded())
                        return entity;

                }
                int lastSectionId = 0;
                bool lastSectionDeleted = false;
                /* process all the passages now */
                List<JArray> updpass = [];
                List<Passage> updpassages = [];
                List<int> delPassageIds = [];
                // collect unique section ids that need their "modified" state updated
                HashSet<int> sectionIdsToUpdate = [];
                List<JArray> delsecItems = [.. data.Where(
                    d => TokToBool(d[0]?["issection"]) && TokToBool(d[0]?["deleted"]) && !TokToBool(d[0]?["complete"])).Cast<JArray>()];

                void ProcessPassage(JArray item)
                {
                    int index = item.Count-1;
                    int? pid = TokToInt(item[index]?["id"]);
                    if (TokToBool(item[index]?["changed"]) && !TokToBool(item[index]?["complete"]))
                    {
                        updpass.Add(item);
                        updpassages.Add(
                            pid.HasValue
                                ? MyRepository
                                    .GetPassage(pid.Value)
                                    .UpdateFrom(item[index], lastSectionId)
                                : new Passage().UpdateFrom(item[index], lastSectionId)
                        );
                        item[index]["complete"] = true;
                    }
                    else if (TokToBool(item[index]?["deleted"]) && !lastSectionDeleted && !TokToBool(item[index]?["complete"]) && pid.HasValue)
                    {
                        delPassageIds.Add(pid.Value);
                        item[index]["complete"] = true;
                    }
                }
                async Task ProcessPassageBatch()
                {
                    // Temporarily disable AutoDetectChanges to speed up bulk operations
                    bool oldAutoDetect = dbContext.ChangeTracker.AutoDetectChangesEnabled;
                    dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                    try
                    {
                        if (updpassages.Count > 0)
                        {
                            _ = MyRepository.BulkUpdatePassages(updpassages);
                            int ix = 0;
                            foreach (JArray item in updpass)
                            {
                                item[item.Count - 1]["id"] = updpassages[ix].Id;
                                sectionIdsToUpdate.Add(updpassages[ix].SectionId);
                                ix++;
                            }
                            updpassages = [];
                            updpass = [];
                        }

                        if (delPassageIds.Count > 0)
                        {
                            // delete in chunks to avoid huge SQL IN lists and reduce transaction time
                            const int CHUNK = 500;
                            for (int off = 0; off < delPassageIds.Count; off += CHUNK)
                            {
                                List<int> chunk = delPassageIds.Skip(off).Take(CHUNK).ToList();
                                _ = await MyRepository.BulkDeletePassagesByIds(chunk);
                            }
                            delPassageIds = [];
                        }
                    }
                    finally
                    {
                        dbContext.ChangeTracker.AutoDetectChangesEnabled = oldAutoDetect;
                    }
                }
                async Task UpdateModifiedPassageSections()
                {
                    //now remove the ones we're going to delete soon anyway
                    IEnumerable<int> delIds = delsecItems
                    .Select(item => TokToInt(item[0]?["id"]))
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value);
                    sectionIdsToUpdate.ExceptWith(delIds);

                    // update each section's modified state once in a single DB call
                    if (sectionIdsToUpdate.Count > 0)
                    {
                        string origin = DbContextExtentions.GetFingerprint(HttpContext);
                        await dbContext.Sections
                            .Where(s => sectionIdsToUpdate.Contains(s.Id))
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(x => x.DateUpdated, DateTime.UtcNow)
                                .SetProperty(x => x.LastModifiedOrigin, origin)
                            );
                    }
                }

                int batchCount = 0;
                foreach (JArray item in data.Cast<JArray>())
                {
                    if (TokToBool(item[0]?["issection"]))
                    {
                        int? s = TokToInt(item[0]?["id"]);
                        if (s.HasValue) //saving in chunks may not have saved this section...passages will be marked unchanged
                        {
                            lastSectionId = s.Value;
                            lastSectionDeleted = TokToBool(item[0]?["deleted"]);
                            if (item.Count > 1)
                            {
                                batchCount++;
                                ProcessPassage(item);
                            }
                        }
                        if (batchCount > 50)
                        {
                            await ProcessPassageBatch();
                            batchCount = 0;
                            if (await BailIfNeeded())
                            {
                                //after the transaction is committed but that's ok
                                await UpdateModifiedPassageSections();
                                return entity;
                            }
                        }
                    }
                    // process any top-level passages that are not under a section
                    else
                    {
                        batchCount++;
                        ProcessPassage(item);
                    }
                }
                await ProcessPassageBatch();
                await UpdateModifiedPassageSections();

                if (await BailIfNeeded())
                    return entity;

                if (delsecItems.Count > 0)
                {
                    int processed = 0;
                    const int archiveBatchSize = 10; // process archived updates in chunks of 10
                    for (int si = 0; si < delsecItems.Count; si += archiveBatchSize)
                    {
                        List<JArray> batchItems = delsecItems.Skip(si).Take(archiveBatchSize).ToList();

                        // mark items with no id as complete immediately
                        foreach (JArray bi in batchItems)
                        {
                            int? nid = TokToInt(bi[0]?["id"]);
                            if (!nid.HasValue)
                            {
                                bi[0]["complete"] = true;
                                processed++;
                            }
                        }

                        List<int> ids = batchItems
                            .Select(item => TokToInt(item[0]?["id"]))
                            .Where(id => id.HasValue)
                            .Select(id => id!.Value)
                            .ToList();

                        if (ids.Count == 0)
                        {
                            // nothing to do for this batch
                            if (await BailIfNeeded())
                            {
                                return entity;
                            }
                            continue;
                        }

                        try
                        {
                            int updated = await dbContext.Sections
                                .Where(s => ids.Contains(s.Id))
                                .ExecuteUpdateAsync(s => s
                                    .SetProperty(x => x.Archived, true)
                                    .SetProperty(x => x.DateUpdated, DateTime.UtcNow)
                                    .SetProperty(x => x.LastModifiedOrigin, "archive")
                                );

                            // mark batch items complete
                            foreach (JArray bi in batchItems)
                            {
                                int? bid = TokToInt(bi[0]?["id"]);
                                if (bid.HasValue)
                                {
                                    bi[0]["complete"] = true;
                                    processed++;
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            // If this is a Postgres lock/timeout error, log and continue so other sections can be processed.
                            string exType = ex.GetType().FullName ?? string.Empty;
                            if (exType == "Npgsql.PostgresException" || ex.Message?.IndexOf("lock", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Message?.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // leave items incomplete so they will be retried later
                                continue;
                            }
                            else
                            {
                                // unexpected error - log and rethrow to abort processing
                                Logger.LogError(ex, "SPX Archiving section batch starting at {si} failed unexpectedly", si);
                                throw;
                            }
                        }

                        // check bail after each batch
                        if (await BailIfNeeded())
                        {
                            return entity;
                        }
                    }

                }
                _ = MyRepository.UpdatePlanModified(entity.PlanId);
                transaction.Commit();
                entity.Data = JsonConvert.SerializeObject(data);
                entity.Complete = true;
                entity.Processing = false;
                // finished processing, clear the processing claim
                await UpdateIt(entity);

                return entity;
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "SPX Insert Error while processing Sectionpassage.Id={Id}", entity.Id);
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
        private async Task UpdateIt(Sectionpassage entity)
        {
            if (entity.Id != 0)
            {
                await dbContext.Sectionpassages
                    .Where(x => x.Id == entity.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Data, x => entity.Data)
                    .SetProperty(x => x.Processing, x => entity.Processing)
                    .SetProperty(x => x.Complete, x => entity.Complete)
                    .SetProperty(x => x.ProcessingStarted, x => entity.Processing ? DateTime.UtcNow : (DateTime?)null)
                    .SetProperty(x => x.DateUpdated, x => DateTime.UtcNow)
                );
            }
        }
        private async Task ClaimIt(Sectionpassage entity)
        {
            // not currently processing: claim it
            entity.Processing = true;
            entity.Complete = false;
            entity.ProcessingStarted = DateTime.UtcNow; //set it here for first claim where id is not yet known
            await UpdateIt(entity);
        }
        private async Task<bool> DidIClaimIt(Sectionpassage existing)
        {
            const int PROCESSING_STALE_SECONDS = 32;
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
                    Logger.LogError(ex, "CreateAsync failed for Sectionpassage UUID={Uuid}", entity.Uuid);
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
