using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.HttpContextHelpers;

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

        private async Task<bool> PublishSection(Section section)
        {

            if (PublishToAkuo(section.PublishTo))
            {
                //find the book and altbook and make sure the titles are published -- may not have had the bible set before
                //maybe they're in another plan...but not handling that for now
                int planId = section.Plan?.Id ?? section.PlanId;
                List<Section> books = dbContext.Sections.Where(s => s.PlanId == planId && s.Sequencenum < 0 ).ToList();
                foreach (Section booksection in books)
                {
                    await PublishTitle(booksection, booksection);
                }
            }
            if (section.PublishTo?.Contains("Propagate") ?? false)
            {
                HttpContext?.SetFP("publish");
                await PublishPassages(section.Id, section.PublishTo ?? "{}");
                return true;
            }
            return false;
        }
        private async Task PublishPassages(int sectionid, string publishTo)
        {
            bool publish = PublishToAkuo(publishTo) || PublishAsSharedResource(publishTo);
            List<Passage> passages = [.. dbContext.Passages.Where(p => p.SectionId == sectionid)];
            foreach (Passage? passage in passages)
            {
                //do not do linked notes...only if this note is the source.  This query will include notes that are the source
                IOrderedQueryable<Mediafile> mediafiles = dbContext.Mediafiles
                        .Where(m => m.PassageId == passage.Id && m.ArtifactTypeId == null && !m.Archived)
                        .OrderByDescending(m => m.VersionNumber);
                List < Mediafile > medialist = publish && mediafiles.Any()
                ?
                [
                    mediafiles.FirstOrDefault()
                ]
                : [.. mediafiles];
                if (publish)
                {
                    //turn the others off
                    IQueryable<Mediafile> on = mediafiles.Where(m => m.ReadyToShare == true);
                    foreach (Mediafile m in on)
                    {
                        m.ReadyToShare = false;
                        dbContext.Mediafiles.Update(m);
                    }
                }
                foreach (Mediafile mediafile in medialist)
                {
                    if (publish)
                        await MediafileRepository.Publish(mediafile.Id, publishTo, false);
                    else if (mediafile.ReadyToShare && (publishTo != "{}"))
                    {
                        JObject pt = JObject.Parse(publishTo);
                        pt.Add("PublishPassage", false);
                        mediafile.ReadyToShare = false;
                        mediafile.PublishTo = pt.ToString();
                        dbContext.Mediafiles.Update(mediafile);
                    }
                };
                _ = dbContext.SaveChanges();
            }
        }
        private async Task PublishTitle(Section resourceFromRequest, Section resourceFromDatabase)
        {
            int? titleMedia = resourceFromRequest.TitleMediafileId ?? resourceFromRequest.TitleMediafile?.Id ?? resourceFromDatabase.TitleMediafileId ?? resourceFromDatabase.TitleMediafile?.Id;
            if (titleMedia != null) //always do titles and movements
                await MediafileRepository.Publish((int)titleMedia, "{\"Public\": \"true\"}", true);
        }
        public override async Task CreateAsync(Section resourceFromRequest, Section resourceForDatabase, CancellationToken cancellationToken)
        {
            await CheckPublish(resourceFromRequest, resourceForDatabase);
            await base.CreateAsync(resourceFromRequest, resourceForDatabase, cancellationToken);
        }
        public async Task CheckPublish(Section resourceFromRequest, Section resourceFromDatabase)
        {
            try
            {
                await PublishTitle(resourceFromRequest, resourceFromDatabase);
            }
            catch (Exception ex)
            {
                if (ex.Message != "no bible")
                    throw;
            }
            if ((resourceFromRequest.PublishTo ?? "{}") != "{}")//&& resourceFromDatabase.PublishTo != resourceFromRequest.PublishTo)
            {
                await PublishSection(resourceFromRequest);
            }

            int? titleMedia = resourceFromRequest.TitleMediafileId ?? resourceFromDatabase.TitleMediafileId;
            if (titleMedia != null) //always do titles and movements
                await MediafileRepository.Publish((int)titleMedia, "{\"Public\": \"true\"}", true);
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
            Section[] sections = [.. dbContext.Sections.Where(s => ids.Contains(s.Id.ToString()))];
            foreach (Section section in sections)
            {
                section.OrganizationSchemeId = scheme;
            }
            dbContext.Sections.UpdateRange(sections);
            dbContext.SaveChanges();
            return sections;
        }
    }
}
