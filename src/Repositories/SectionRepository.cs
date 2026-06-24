using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class SectionRepository(
        IHttpContextAccessor httpContextAccessor,
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        PlanRepository planRepository,
        MediafileRepository mediafileRepository,
        ArtifactCategoryRepository artifactCategoryRepository

        ) : BaseRepository<Section>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        readonly private PlanRepository PlanRepository = planRepository;
        readonly private MediafileRepository MediafileRepository = mediafileRepository;
        readonly private ArtifactCategoryRepository ArtifactCategoryRepository = artifactCategoryRepository;
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;

        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Section> UsersSections(
            IQueryable<Section> entities,
            IQueryable<Project>? projects
        )
        {
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return PlansSections(entities, plans);
        }

        public IQueryable<Section> PlansSections(
            IQueryable<Section> entities,
            IQueryable<Plan> plans
        )
        {
            return entities.Join(plans, s => s.PlanId, p => p.Id, (s, p) => s);
        }

        public IQueryable<Section> UsersSections(
            IQueryable<Section> entities,
            IQueryable<Plan>? plans = null
        )
        {
            //this gets just the plans I have access to
            plans ??= PlanRepository.UsersPlans(dbContext.Plans);
            return PlansSections(entities, plans);
        }

        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassages()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passages");
            //no error...but no passages either  return Get().Include(s => s.Passages);
            IQueryable<Section> sections = UsersSections(GetAll().Include(Tables.Passages));
            return sections;
        }
        #endregion
        public IQueryable<Section> ProjectSections(IQueryable<Section> entities, string projectid)
        {
            return PlansSections(entities, PlanRepository.ProjectPlans(dbContext.Plans, projectid));
        }

        #region Overrides
        public override IQueryable<Section> FromCurrentUser(IQueryable<Section>? entities = null)
        {
            return UsersSections(entities ?? GetAll());
        }

        public override IQueryable<Section> FromProjectList(
            IQueryable<Section>? entities,
            string idList
        )
        {
            return ProjectSections(entities ?? GetAll(), idList);
        }
        #endregion
        #region ParatextSync
        public async Task<IList<SectionSummary>> SectionSummary(
            int PlanId,
            string book,
            int chapter
        )
        {
            IList<SectionSummary> ss = [];
            var passagewithsection = dbContext.Passages
                .Join(
                    dbContext.Sections.Where(section => section.PlanId == PlanId),
                    passage => passage.SectionId,
                    section => section.Id,
                    (passage, section) => new { passage, section }
                )
                .Where(x => x.passage.Book == book && x.passage.StartChapter == chapter);
            await passagewithsection
                .GroupBy(p => p.section)
                .ForEachAsync(ps => {
                    SectionSummary newss =
                        new()
                        {
                            section = ps.FirstOrDefault()?.section ?? new(),
                            Book = book,
                            StartChapter = chapter,
                            EndChapter = chapter,
                            StartVerse = ps.Min(a => a.passage.StartVerse??0),
                            EndVerse = ps.Max(a => a.passage.EndVerse??0),
                        };
                    ss.Add(newss);
                });
            return ss;
        }
        #endregion

        private async Task PublishPassages(int sectionid, string publishTo)
        {
            bool publish = PublishToAkuo(publishTo) || PublishAsSharedResource(publishTo);

            if (!publish)
            {
                // Unpublishing for whole section: update all mediafiles for all passages in this section in one DB operation.
                JObject pt = JObject.Parse(publishTo);
                pt.Add("PublishPassage", false);
                string newPublishTo = pt.ToString();

                // Join mediafiles to passages in this section and update in a single DB operation.
                IQueryable<Mediafile> q = dbContext.Mediafiles
                    .Join(
                        dbContext.Passages.Where(p => p.SectionId == sectionid),
                        m => m.PassageId,
                        p => p.Id,
                        (m, p) => m
                    )
                    .Where(m => m.ArtifactTypeId == null && !m.Archived && m.ReadyToShare);

                bool any = await q.AnyAsync();
                if (any)
                {
                    await q.ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.ReadyToShare, false)
                        .SetProperty(m => m.PublishTo, newPublishTo)
                        .SetProperty(m => m.DateUpdated, DateTime.UtcNow)
                        .SetProperty(m => m.LastModifiedOrigin, "publish")
                    );
                }

                return;
            }

            List<Passage> passages = [.. dbContext.Passages.Where(p => p.SectionId == sectionid)];

            foreach (Passage? passage in passages)
            {
                //do not do linked notes...only if this note is the source.  This query will include notes that are the source
                IOrderedQueryable<Mediafile> mediafiles = dbContext.Mediafiles
                        .Where(m => m.PassageId == passage.Id && m.ArtifactTypeId == null && !m.Archived)
                        .OrderByDescending(m => m.VersionNumber);
#pragma warning disable CS8604 // Possible null reference argument.
                List <Mediafile> medialist = publish && mediafiles.Any()
                ?
                [
                    mediafiles.FirstOrDefault()
                ]
                : [.. mediafiles];
#pragma warning restore CS8604 // Possible null reference argument.


                // Publishing path - enqueue publish messages for each mediafile (do not perform long-running Akuo work inline)
                foreach (Mediafile mediafile in medialist)
                {
                    if (mediafile.PublishTo != publishTo || (mediafile.PublishedAs ?? "") == "")
                        // Build minimal mediafile info by reusing repository publish prefetch and then enqueue via MediafileRepository
                        await MediafileRepository.Publish(mediafile, publishTo);
                }
            }
        }
        private async Task PublishTitle(Section resourceFromRequest, Section resourceFromDatabase)
        {
            int? titleMedia = resourceFromRequest.TitleMediafileId ?? resourceFromRequest.TitleMediafile?.Id ?? resourceFromDatabase.TitleMediafileId ?? resourceFromDatabase.TitleMediafile?.Id;
            if (titleMedia != null) //always do titles and movements
                await MediafileRepository.PublishTitle((int)titleMedia);
        }
        public override async Task CreateAsync(Section resourceFromRequest, Section resourceForDatabase, CancellationToken cancellationToken)
        {
            await CheckPublish(resourceFromRequest, resourceForDatabase);
            await base.CreateAsync(resourceFromRequest, resourceForDatabase, cancellationToken);
        }
        public async Task<bool> CheckPublish(Section section, Section resourceFromDatabase)
        {
            try
            {
                await PublishTitle(section, resourceFromDatabase);
            }
            catch (Exception ex)
            {
                if (ex.Message == "no bible")
                    return false;
                else
                    throw;
            }
            if (PublishToAkuo(section.PublishTo))
            {
                //find the book and altbook and make sure the titles are published -- may not have had the bible set before
                //maybe they're in another plan...but not handling that for now
                int planId = section.Plan?.Id ?? section.PlanId;
                List<Section> books = [.. dbContext.Sections.Where(s => s.PlanId == planId && s.Sequencenum < 0 )];
                foreach (Section booksection in books)
                {
                    await PublishTitle(booksection, booksection);
                }
            }
            if (section.PublishTo?.Contains("Propagate") ?? false)
            {
                HttpContext?.SetFP("publish");
                await PublishPassages(section.Id, section.PublishTo ?? "{}");
                JObject obj = JObject.Parse(section.PublishTo ?? "{}");
                obj.Remove("Propagate");
                section.PublishTo = obj.ToString();
                return true;
            }
            return false;
        }
        public override async Task UpdateAsync(Section resourceFromRequest, Section resourceFromDatabase, CancellationToken cancellationToken)
        {
            //if we meant to send it it will have destinationsetbyuser:true in it
            await CheckPublish(resourceFromRequest, resourceFromDatabase);
            await base.UpdateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
        }

        public IEnumerable<Section> AssignSections(int scheme, string idlist)
        {
            string[] ids = idlist.Split('|');
            List<Section> sections = [];
            foreach (string idstr in ids)
            {
                if (int.TryParse(idstr, out int id))
                {
                    Section? section = dbContext.Sections.Find(id);
                    if (section != null)
                    {
                        section.OrganizationSchemeId = scheme;
                        sections.Add(section);
                    }
                }
            }
            dbContext.Sections.UpdateRange(sections);
            dbContext.SaveChanges();
            return sections;
        }
    }
}
