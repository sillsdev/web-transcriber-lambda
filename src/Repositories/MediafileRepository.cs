using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;
using System.Net;
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
        IS3Service service
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
        readonly private PlanRepository PlanRepository = planRepository;
        readonly private ProjectRepository ProjectRepository = projectRepository;
        readonly private IS3Service S3service = service;
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;
        readonly private ArtifactCategoryRepository ArtifactCategoryRepository = artifactCategoryRepository;

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

        public IEnumerable<Mediafile>? WBTUpdate()
        {
            Artifacttype? newAT = dbContext.Artifacttypes.Where(at => at.Typename == "wholebacktranslation").FirstOrDefault();
            if (newAT == null)
                return null;
            IEnumerable<Mediafile>? entities = [.. FromCurrentUser().Join(dbContext.Artifacttypes.Where(a => a.Typename == "backtranslation"), m => m.ArtifactTypeId, a => a.Id, (m, a) => m).Where(m => m.SourceSegments == null)];
            foreach (Mediafile m in entities)
            {
                m.ArtifactTypeId = newAT.Id;
                dbContext.Update(m);
            }
            dbContext.SaveChanges();
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
            if (PublishToAkuo(resourceFromRequest.PublishTo) && resourceFromDatabase.PublishTo != resourceFromRequest.PublishTo)
            {
                Passage? passage = dbContext.PassagesData.SingleOrDefault(p => p.Id == (resourceFromRequest.PassageId ?? 0));
                Sharedresource? sr = passage != null ? GetSharedResource(passage) : null;
                await PublishToAkuo(resourceFromRequest, passage, null, sr);
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
                    : $"{title}{Path.ChangeExtension(m.OriginalFile, ".mp3")}";
            }
            else if (p?.Passagetype?.Abbrev == "CHNUM")
            {
                title = $"{title}{FileName.CleanFileName(p.Reference ?? Path.ChangeExtension(m.OriginalFile, ".mp3") ?? p.Id.ToString())}";
            }
            else
            {
                Section? s = dbContext.SectionsData.Where(s => s.TitleMediafileId == m.Id && !s.Archived).FirstOrDefault();
                if (s != null)
                {
                    title = $"{title}{FileName.CleanFileName(s.Plan?.Name ?? "")}{PadSeqNum(s.Sequencenum)}{FileName.CleanFileName(s.Name)}";
                }
                else if (m.OriginalFile != null)
                    title = $"{title}{FileName.CleanFileName(Path.ChangeExtension(m.OriginalFile, ".mp3"))}";
            }
            return FileName.CleanFileName(title.EndsWith(".mp3") ? title[..^4] : title);
        }
        private static string PublishFilename(string title, string bibleId)
        {
            string fileName = title;

            if (!fileName.EndsWith(".mp3"))
            {
                fileName = $"{fileName}.mp3";
            }
            return $"{bibleId}/{fileName}";
        }
        private string PublishGraphic(Mediafile m)
        {
            Graphic? graphic = null;
            if (m.PassageId != null)
            {
                graphic = dbContext.Graphics.SingleOrDefault(g => g.ResourceId == m.PassageId && g.ResourceType == "passage" && !g.Archived);
                if (graphic == null)
                {
                    int sectionId = m.Passage?.SectionId ?? 0;
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

        public Sharedresource? CreateSharedResource(Mediafile m, Passage p)
        {
            string fp = HttpContext?.GetFP() ?? "";
            HttpContext?.SetFP("publish");
            Plan? plan = PlanRepository.GetWithProject(m.PlanId) ?? throw new Exception("no plan");
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
            HttpContext?.SetFP(fp);
            return sr;
        }
        private async Task<bool> PublishToAkuo(Mediafile m, Passage? passage, Bible? bible, Sharedresource? sr)
        {
            Plan? plan = PlanRepository.GetWithProject(m.PlanId) ?? throw new Exception("no plan");
            bible ??= PlanRepository.Bible(plan) ?? throw new Exception("no bible");
            string title =  PublishTitlename(m,passage, false);
            string outputKey = PublishFilename(title, bible.BibleId);
            string inputKey = $"{PlanRepository.DirectoryName(plan)}/{m.S3File ?? ""}";

            if (sr != null)
            {
                int? titleMedia = sr.TitleMediafile?.Id ?? sr.TitleMediafileId;
                if (titleMedia != null)
                    await Publish((int)titleMedia, "{\"Public\": \"true\"}", false);
                if (sr.ArtifactCategory?.TitleMediafileId != null)
                {
                    Bible? acbible = ArtifactCategoryRepository.GetBible(sr.ArtifactCategory);
                    await Publish((int)sr.ArtifactCategory.TitleMediafileId, "{\"Public\": \"true\"}", false, acbible);
                }
            }


            Tags tags = new()
            {
                title = title,
                artist = m.PerformedBy??"",
                album = bible.BibleName,
                cover= PublishGraphic(m),
            };
            S3Response response = await S3service.CreatePublishRequest(m.Id, inputKey, outputKey, JsonConvert.SerializeObject(tags));
            //Logger.LogCritical("XXX create request {status} {ok} {outputKey}", response.Status, response.Status == HttpStatusCode.OK, outputKey);
            if (response.Status == HttpStatusCode.OK)
            {
                m.PublishedAs = outputKey;
                m.ReadyToShare = true;
                return true;
            }
            return false;
        }
        public async Task<Mediafile?> Publish(int id, string publishTo, bool doSave, Bible? bible = null)
        {
            try
            {
                Mediafile? m = Get(id);

                if (m == null)
                {
                    return null;
                }
                Passage? passage = dbContext.PassagesData.SingleOrDefault(p => p.Id == (m.PassageId ?? 0));
                Sharedresource? sr = passage != null ? GetSharedResource(passage) : null;

                if (PublishAsSharedResource(publishTo) && passage != null && (passage.Passagetype is null || passage?.Passagetype?.Abbrev == "NOTE"))
                {
                    sr ??= CreateSharedResource(m, passage);
                }
                if (PublishToAkuo(publishTo))
                {
                    Plan? plan = PlanRepository.GetWithProject(m.PlanId) ?? throw new Exception("no plan");
                    bible ??= PlanRepository.Bible(plan) ?? throw new Exception("no bible");
                    string title =  PublishTitlename(m,passage, false);
                    string outputKey = PublishFilename(title, bible.BibleId);
                    string inputKey = $"{PlanRepository.DirectoryName(plan)}/{m.S3File ?? ""}";
                    Tags tags = new()
                    {
                        title = title,
                        artist = m.PerformedBy??"",
                        album = bible.BibleName,
                        cover= PublishGraphic(m),
                    };
                    S3Response response = await S3service.CreatePublishRequest(m.Id, inputKey, outputKey, JsonConvert.SerializeObject(tags));
                    //Logger.LogCritical("XXX create request {status} {ok} {outputKey}", response.Status, response.Status == HttpStatusCode.OK, outputKey);
                    if (response.Status == HttpStatusCode.OK)
                    {
                        m.PublishedAs = outputKey;
                        m.ReadyToShare = true;
                        m.PublishTo = publishTo;
                        try
                        {
                            dbContext.Update(m);
                            //if (doSave)
                            dbContext.SaveChanges();
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err);
                        }
                    }
                }
                else if (!m.ReadyToShare || m.PublishTo != publishTo)
                {
                    m.ReadyToShare = true;

                    return null;
                }
                if (PublishToAkuo(publishTo) && m.PublishedAs == null)
                {
                    if (await PublishToAkuo(m, passage, bible, sr))
                    {
                        m.PublishTo = publishTo;
                        dbContext.Mediafiles.Update(m);
                        if (doSave)
                            dbContext.SaveChanges();
                    }
                }
                return m;
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
                return null;
            }

        }
    }
}
