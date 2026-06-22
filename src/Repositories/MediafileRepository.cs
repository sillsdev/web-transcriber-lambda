using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Services.Contracts;
using SIL.Transcriber.Utility;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.HttpContextHelpers;

namespace SIL.Transcriber.Repositories
{
    public class MediafileRepository(
        IHttpContextAccessor httpContextAccessor,
        ITargetedFields targetedFields,
        AppDbContextResolver _contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        PlanRepository planRepository,
        ProjectRepository projectRepository,
        ArtifactCategoryRepository artifactCategoryRepository,
        IS3Service service,
        ISQSService sqsService
        ) : BaseRepository<Mediafile>(
            targetedFields,
            _contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        private const string PUBLISHED_EXTENSION = ".mp3";
        readonly private PlanRepository PlanRepository = planRepository;
        readonly private ProjectRepository ProjectRepository = projectRepository;
        readonly private IS3Service S3service = service;
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;
        readonly private ArtifactCategoryRepository ArtifactCategoryRepository = artifactCategoryRepository;
        readonly private ISQSService SQSservice = sqsService;

        public IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, int project)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == project);
            return UsersMediafiles(entities, projects);
        }

        //get my Mediafiles in these projects
        public IQueryable<Mediafile> UsersMediafiles(
            IQueryable<Mediafile> entities,
            IQueryable<Project> projects
        )
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersMediafiles(entities, plans);
        }

        private static IQueryable<Mediafile> PlansMediafiles(
            IQueryable<Mediafile> entities,
            IQueryable<Plan> plans
        )
        {
            return entities.Join(plans, m => m.PlanId, p => p.Id, (m, p) => m);
        }

        public async Task<IEnumerable<Mediafile>?> WBTUpdate()
        {
            Artifacttype? newAT = dbContext.Artifacttypes.Where(at => at.Typename == "wholebacktranslation").FirstOrDefault();
            if (newAT == null)
                return null;

            // Bulk update in single SQL statement - avoids N+1 updates
            // ExecuteUpdateAsync executes immediately (not deferred to SaveChanges)
            int newATId = newAT.Id;
            await FromCurrentUser()
                .Join(dbContext.Artifacttypes.Where(a => a.Typename == "backtranslation"),
                      m => m.ArtifactTypeId, a => a.Id, (m, a) => m)
                .Where(m => m.SourceSegments == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ArtifactTypeId, newATId)
                            .SetProperty(m => m.DateUpdated, DateTime.UtcNow)
                            .SetProperty(m => m.LastModifiedOrigin, "wbt"));

            // Return updated entities for response
            IEnumerable<Mediafile>? entities = [.. FromCurrentUser()
                .Join(dbContext.Artifacttypes.Where(a => a.Typename == "wholebacktranslation"),
                      m => m.ArtifactTypeId, a => a.Id, (m, a) => m)
                .Where(m => m.SourceSegments == null)];
            return entities;
        }
        private IQueryable<Mediafile> UsersMediafiles(
            IQueryable<Mediafile> entities,
            IQueryable<Plan>? plans = null
        )
        {
            plans ??= PlanRepository.UsersPlans(dbContext.Plans);

            return PlansMediafiles(entities, plans);
        }

        public IQueryable<Mediafile> ProjectsMediafiles(
            IQueryable<Mediafile> entities,
            string idlist
        )
        {
            IQueryable<Project> projects = ProjectRepository.FromIdList(dbContext.Projects, idlist);
            IQueryable<Plan> plans = PlanRepository.ProjectPlans(dbContext.Plans, projects);
            return PlansMediafiles(entities, plans);
        }

        public Mediafile? GetLatestShared(int passageId)
        {
            return GetAll()
                .Where(p => p.PassageId == passageId && p.ReadyToShare && !p.Archived)
                .OrderBy(m => m.VersionNumber)
                .LastOrDefault();
        }
        public Mediafile? GetLatestForPassage(int passageId)
        {
            return GetAll()
                .Where(p => p.PassageId == passageId && !p.Archived)
                .OrderBy(m => m.VersionNumber)
                .LastOrDefault();
        }
        public IEnumerable<Mediafile> PassageReadyToSync(int PassageId, int artifactTypeId = 0)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            IEnumerable<Mediafile> media =
                artifactTypeId == 0 ?
                    dbContext.Mediafiles
                    .Where(m => m.PassageId == PassageId
                             && m.ArtifactTypeId == null && !m.Archived)
                    .Include(m => m.Passage)
                    .ThenInclude(p => p.Section)
                    .ThenInclude(s => s.Plan)
                    .ThenInclude(p => p.Project)
                    .OrderBy(m => m.VersionNumber)
                :
                    dbContext.Mediafiles
                    .Where(m =>
                            m.PassageId == PassageId
                            && m.ArtifactTypeId == artifactTypeId && !m.Archived)
                    .Include(m => m.Passage)
                    .ThenInclude(p => p.Section)
                    .ThenInclude(s => s.Plan)
                    .ThenInclude(p => p.Project)
                    .ToList()
                    .Where(m => m.ReadyToSync)
                    .OrderBy(m => m.VersionNumber);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            if (artifactTypeId == 0)
            {
                List<Mediafile> ret = [];
                if (media.Any() && media.Last().ReadyToSync)
                    ret.Add(media.Last());
                return ret;
            }
            return media;
        }


        public IEnumerable<Mediafile> ReadyToSync(int PlanId, int artifactTypeId = 0)
        {
            //this should disqualify media that has a new version that isn't ready...but doesn't (yet)
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            IEnumerable<Mediafile> media = dbContext.Mediafiles
                .Where(m =>
                        m.PlanId == PlanId
                        && (
                            artifactTypeId == 0
                                ? m.ArtifactTypeId == null
                                : m.ArtifactTypeId == artifactTypeId
                        )
                        && m.PassageId != null
                )
                .Include(m => m.Passage)
                .ThenInclude(p => p.Section)
                .ThenInclude(s => s.Plan)
                .ThenInclude(p => p.Project)
                .ToList()
                .Where(m => m.ReadyToSync)
                .OrderBy(m => m.PassageId)
                .ThenBy(m => m.VersionNumber);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            return media;
        }

        public override IQueryable<Mediafile> FromCurrentUser(
            IQueryable<Mediafile>? entities = null
        )
        {
            return UsersMediafiles(entities ?? GetAll());
        }

        //handles PROJECT_SEARCH_TERM and PROJECT_LIST
        public override IQueryable<Mediafile> FromProjectList(
            IQueryable<Mediafile>? entities,
            string idList
        )
        {
            return ProjectsMediafiles(entities ?? GetAll(), idList);
        }

        public Mediafile? Get(int id)
        {
            return dbContext.MediafilesData.SingleOrDefault(p => p.Id == id);
        }

        public override async Task CreateAsync(
            Mediafile resourceFromRequest,
            Mediafile resourceForDatabase,
            CancellationToken cancellationToken
        )
        {
            //copy the values we set manually in the service CreateAsync
            resourceForDatabase.S3File = resourceFromRequest.S3File;
            resourceForDatabase.AudioUrl = resourceFromRequest.AudioUrl;
            resourceForDatabase.ContentType = resourceFromRequest.ContentType;
            resourceForDatabase.EafUrl = resourceFromRequest.EafUrl;
            await base.CreateAsync(resourceFromRequest, resourceForDatabase, cancellationToken);
        }
        public override async Task UpdateAsync(
                Mediafile resourceFromRequest,
                Mediafile resourceFromDatabase,
                CancellationToken cancellationToken
            )
        {
            //if we have a duration, don't set it back to null
            if ((resourceFromRequest.Duration ?? 0) == 0 && (resourceFromDatabase.Duration ?? 0) != 0)
            {
                //if the duration is 0, then we need to update it
                resourceFromRequest.Duration = resourceFromDatabase.Duration;
            }
            if (resourceFromRequest.ReadyToShare && resourceFromDatabase.PublishTo != resourceFromRequest.PublishTo)
            {
                await Publish(resourceFromRequest, resourceFromRequest.PublishTo ?? "{}");
            }
            await base.UpdateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
        }
        private static string PadSeqNum(decimal? seq)
        {
            if (seq == null)
                return "";
            string[]? parts = seq?.ToString().Split(".");
            return parts?[0].PadLeft(3, '0') + (parts?.Length > 1 ? "." + parts[1] : "");
        }
        private string PublishTitlename(Mediafile m, Passage? p, bool pretty)
        {
            string book = p?.Book ?? "";
            string title = book;
            if (!pretty)
                title += $"{PadSeqNum(p?.Section?.Sequencenum)}{PadSeqNum(p?.Sequencenum)}";
            if (title.Length > 0)
                title += pretty ? " " : "_";
            if (p?.StartChapter != null)
            {
                string separator =  pretty ? " " : "_";
                string chapsep = pretty ? ":" : "_";
                string startChap = p.StartChapter?.ToString().PadLeft(3, '0') ?? "";
                string endChap = p.EndChapter?.ToString().PadLeft(3, '0') ?? "";
                string startVerse = p.StartVerse?.ToString().PadLeft(3, '0') ?? "";
                string endVerse = (p.EndVerse ?? p.StartVerse)?.ToString().PadLeft(3, '0') ?? "";
                title = startChap == endChap ?
                    startVerse == endVerse ?
                        $"{title}c{startChap}{chapsep}{startVerse}" :
                        $"{title}c{startChap}{chapsep}{startVerse}-{endVerse}"
                    : $"{title}c{startChap}{chapsep}{startVerse}-c{endChap}{chapsep}{endVerse}";
            }
            else if (p?.Passagetype?.Abbrev == "NOTE")
            {
                Sharedresource? sr = dbContext.SharedresourcesData.SingleOrDefault(sr => sr.Id == p.SharedResourceId);
                sr ??= dbContext.SharedresourcesData.SingleOrDefault(sr => sr.PassageId == p.Id && !sr.Archived);
                title = (sr?.Title ?? "") != ""
                    ? $"{title}NOTE_{FileName.CleanFileName(sr?.Title ?? "")}"
                    : $"{title}{Path.ChangeExtension(m.OriginalFile, PUBLISHED_EXTENSION)}";
            }
            else if (p?.Passagetype?.Abbrev == "CHNUM")
            {
                title = $"{title}{FileName.CleanFileName(p.Reference ?? Path.ChangeExtension(m.OriginalFile, PUBLISHED_EXTENSION) ?? p.Id.ToString())}";
            }
            else
            {
                Section? s = dbContext.SectionsData.Where(s => s.TitleMediafileId == m.Id && !s.Archived).FirstOrDefault();
                if (s != null)
                {
                    title = $"{title}{FileName.CleanFileName(s.Plan?.Name ?? "")}{PadSeqNum(s.Sequencenum)}{FileName.CleanFileName(s.Name)}";
                }
                else if (m.OriginalFile != null)
                    title = $"{title}{FileName.CleanFileName(Path.ChangeExtension(m.OriginalFile, PUBLISHED_EXTENSION))}";
            }
            return FileName.CleanFileName(title.EndsWith(PUBLISHED_EXTENSION) ? title[..^4] : title);
        }
        private static string PublishFilename(string title, string bibleId)
        {
            string fileName = title;

            if (!fileName.EndsWith(PUBLISHED_EXTENSION))
            {
                fileName = $"{fileName}{PUBLISHED_EXTENSION}";
            }
            return $"{bibleId}/{fileName}";
        }
        private string PublishGraphic(Mediafile m, Passage? passage)
        {
            Graphic? graphic = null;
            if (m.PassageId != null)
            {
                graphic = dbContext.Graphics.SingleOrDefault(g => g.ResourceId == m.PassageId && g.ResourceType == "passage" && !g.Archived);
                if (graphic == null)
                {
                    int sectionId = passage?.SectionId ?? 0;
                    graphic = dbContext.Graphics.SingleOrDefault(g => g.ResourceId == sectionId && g.ResourceType == "section" && !g.Archived);
                }
            }
            if (graphic == null)
            {
                Section? s = dbContext.SectionsData.SingleOrDefault(s => s.TitleMediafileId == m.Id && !s.Archived);
                if (s != null)
                    graphic = dbContext.Graphics.SingleOrDefault(g => g.ResourceId == s.Id && g.ResourceType == "section" && !g.Archived);
            }
            dynamic? json = JsonConvert.DeserializeObject(graphic?.Info ?? "{}");
            return json?["512"]?["content"] ?? "";
        }

        private Sharedresource? GetSharedResource(Passage p)
        {
            Sharedresource? sr = dbContext.SharedresourcesData.SingleOrDefault(sr => sr.Id == p.SharedResourceId); //linked note
            sr ??= dbContext.SharedresourcesData.SingleOrDefault(sr => sr.PassageId == p.Id); //source note
            return sr;
        }

        public Sharedresource? CreateSharedResource(Mediafile m, Passage p, Plan? plan = null)
        {
            plan ??= PlanRepository.GetWithProject(m.PlanId) ?? throw new Exception("no plan");
            Artifactcategory? ac = null;
            if (p.Passagetype == null && plan.Project.Projecttype.Name == "Scripture")
                ac = dbContext.ArtifactcategoriesData.SingleOrDefault(ac => !ac.Archived && ac.OrganizationId == null && ac.Categoryname == "scripture");
            Sharedresource sr = new ()
            {
                PassageId = m.PassageId,
                Title = PublishTitlename(m, p, true),
                Description = "",
                Languagebcp47 = $"{plan.Project.LanguageName??""}|{plan.Project.Language}",
                ArtifactCategoryId = ac?.Id,
                Note = p.PassagetypeId != null,
            };
            dbContext.Sharedresources.Add(sr);
            dbContext.SaveChanges();
            Sharedresourcereference srr = new ()
            {
                SharedResourceId = sr.Id,
                Book = p.Book ?? "",
            };
            string verses = "";
            if (p.StartChapter != null)
            {
                srr.Chapter = (int)p.StartChapter;
                if (p.StartChapter == p.EndChapter)
                {
                    for (int ix = p.StartVerse ?? 0; ix <= (p.EndVerse ?? -1); ix++)
                        verses += ix.ToString() + ",";
                    srr.Verses = verses[..^1];
                    dbContext.Sharedresourcereferences.Add(srr);
                }
                else
                {
                    srr.Verses = p.StartVerse?.ToString() ?? "";
                    dbContext.Sharedresourcereferences.Add(srr);
                    Sharedresourcereference srr2 = new ()
                    {
                        SharedResourceId = sr.Id,
                        Book = p.Book ?? "",
                        Chapter = p.EndChapter??0,
                    };
                    for (int ix = 1; ix <= (p.EndVerse ?? -1); ix++)
                        verses += ix.ToString() + ",";
                    srr2.Verses = verses[..^1];
                    dbContext.Sharedresourcereferences.Add(srr2);
                }
            }
            dbContext.SaveChanges();
            return sr;
        }
        private void QueuePublish(Mediafile m, string publishTo, Passage? passage, Bible bible, Sharedresource? sr, Plan plan)
        {
            string title = PublishTitlename(m, passage, false);
            string outputKey = PublishFilename(title, bible.BibleId);
            string inputKey = $"{PlanRepository.DirectoryName(plan)}/{m.S3File ?? ""}";
            Tags tags = new()
            {
                title = title,
                artist = m.PerformedBy ?? "",
                album = bible.BibleName,
                cover = PublishGraphic(m, passage),
            };
            // Build the mediafile message as specified
            var mediaMsg = new
            {
                type = "mediafile",
                id = m.Id,
                publishTo,
                sharedResourceTitle = sr?.Title ?? "",
                akuo = new
                {
                    inputKey,
                    outputKey,
                    tags = new
                    {
                        tags.title,
                        tags.artist,
                        tags.album,
                        tags.cover,
                    }
                },
                passageId = passage?.Id,
                planId = plan.Id,
                languagebcp47 = sr?.Languagebcp47 ?? $"{plan.Project.LanguageName ?? ""}|{plan.Project.Language}",
                sharedResourceId = sr?.Id,
                passage = passage == null ? null : new
                {
                    passagetypeId = passage.PassagetypeId,
                    passagetypeAbbrev = passage.Passagetype?.Abbrev,
                    book = passage.Book,
                    startChapter = passage.StartChapter,
                    endChapter = passage.EndChapter,
                    startVerse = passage.StartVerse,
                    endVerse = passage.EndVerse
                },
                forceFileRefresh=false
            };

            string body = JsonConvert.SerializeObject(mediaMsg);
            string url = GetVarOrThrow("SIL_TR_PUBLISH_QUEUE");
            string sendResult = SQSservice.SendMessage(url, body, "nodup", m.Id.ToString());
            if (sendResult == "error")
                throw new Exception("Failed to enqueue publish message");
        }
        public async Task<Mediafile?> Publish(Mediafile m, string publishTo, Bible? bible = null, Plan? plan = null, Sharedresource? sr = null)
        {
            if (publishTo == "{}")
                return m;
            try
            {
                // Performance logging to find bottlenecks

                //string fp = HttpContext?.GetFP() ?? "";
                Passage? passage = dbContext.PassagesData.SingleOrDefault(p => p.Id == (m.PassageId ?? 0));
                sr ??= passage != null ? GetSharedResource(passage) : null;
                plan ??= PlanRepository.GetWithProject(m.PlanId) ?? throw new Exception("no plan");
                bible ??= PlanRepository.Bible(plan) ?? throw new Exception("no bible");

                QueuePublish(m, publishTo, passage, bible, sr, plan);

                // Persist minimal state quickly
                await dbContext.Mediafiles
                    .Where(x => x.Id == m.Id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.ReadyToShare, true)
                        .SetProperty(x => x.PublishTo, publishTo)
                        .SetProperty(m => m.DateUpdated, DateTime.UtcNow)
                        .SetProperty(m => m.LastModifiedOrigin, "publish")
                    );

                m.ReadyToShare = true;
                m.PublishTo = publishTo;
                Logger.LogInformation("Publish {MediafileId}: finished main work - ReadyToShare={Ready} PublishTo={PublishTo}", m.Id, m.ReadyToShare, m.PublishTo);
                return m;
            }
            catch (Exception err)
            {
                Logger.LogError(err, "Publish {MediafileId}: error", m?.Id ?? 0);
                return null;
            }
        }
        public async Task<Mediafile?> PublishTitle(int id, Bible? bible = null, Sharedresource? sr = null)
        {
            return await Publish(id, "{\"Public\": \"true\"}", bible, sr);
        }
        public async Task<Mediafile?> Publish(int id, string publishTo, Bible? bible = null, Sharedresource? sr = null)
        {
            try
            {
                Mediafile? m = dbContext.Mediafiles.Find(id);
                if (m == null)
                    return null;
                HttpContext?.SetFP("publish");
                m = await Publish(m, publishTo, bible, null, sr);
                return m;
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return null;
            }

        }

        public IEnumerable<SimpleResponse> GetFixDuration()
        {
            return dbContext.MediafilesData.Where(m => !m.Archived && (m.ContentType ?? "").StartsWith("audio") &&
                                                    (m.Duration == null || m.Duration == 0))
                                           .OrderBy(m => m.Id)
                                           .Take(500)
                                           .ToList()
                                           .Select(m => new SimpleResponse
                                           {
                                               Id = m.Id,
                                               Message = S3service.SignedUrlForGet(m.S3File ?? "", m.S3Folder ?? PlanRepository.DirectoryName(m.PlanId), m.ContentType ?? "").Message
                                           });
        }
    }
}
