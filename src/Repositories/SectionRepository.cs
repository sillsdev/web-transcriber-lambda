using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.HttpContextHelpers;

namespace SIL.Transcriber.Repositories
{
    public class SectionRepository : BaseRepository<Section>
    {
        readonly private PlanRepository PlanRepository;
        readonly private MediafileRepository MediafileRepository;
        readonly private HttpContext? HttpContext;
        public SectionRepository(
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
            MediafileRepository mediafileRepository

        )
            : base(
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
            PlanRepository = planRepository;
            MediafileRepository = mediafileRepository;
            HttpContext = httpContextAccessor.HttpContext;
        }

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
            IList<SectionSummary> ss = new List<SectionSummary>();
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

        private async Task PublishSection(Section section, bool publish)
        {
            string fp = HttpContext != null ? HttpContext.GetFP() ?? "" : "";
            HttpContext?.SetFP("publish");
            await PublishPassages(section.Id, section.PublishTo, publish);
        }
        private async Task PublishPassages(int sectionid, string publishTo, bool publish)
        {
            List<Passage> passages = dbContext.Passages.Where(p => p.SectionId == sectionid).ToList();
            foreach (Passage? passage in passages)
            {
                IOrderedQueryable<Mediafile> mediafiles = dbContext.Mediafiles
                        .Where(m => m.PassageId == passage.Id && m.ArtifactTypeId == null && !m.Archived)
                        .OrderByDescending(m => m.VersionNumber);
#pragma warning disable CS8604 // Possible null reference argument.
                List < Mediafile > medialist = publish && mediafiles.Any()
                ? new List<Mediafile>
                {
                    mediafiles.FirstOrDefault()
                }
                : mediafiles.ToList();
                //if we are publishing, turn on shared notes.  If not publishing, leave them as they are
                if (publish && passage.SharedResourceId != null)
                {
                    Sharedresource? note = dbContext.Sharedresources.Where(n => n.Id == passage.SharedResourceId).FirstOrDefault();
                    if (note != null)
                    {
                        int? notepsgid = note.PassageId;
                        Mediafile? notemediafile = dbContext.Mediafiles
                            .Where(m => m.PassageId == notepsgid && m.ArtifactTypeId == null && !m.Archived)
                            .OrderByDescending(m => m.VersionNumber).FirstOrDefault();
                        if (notemediafile != null)
                            medialist.Add(notemediafile);
                    }
                }
#pragma warning restore CS8604 // Possible null reference argument.
                foreach(Mediafile mediafile in medialist)
                {
                    if (publish)
                        await MediafileRepository.Publish(mediafile.Id, publishTo, false);
                    else if (mediafile.ReadyToShare)
                    {
                        mediafile.ReadyToShare = false;
                        mediafile.PublishTo = "{}";
                        mediafile.PublishedAs = "";
                        dbContext.Mediafiles.Update(mediafile);
                    }
                };
                dbContext.SaveChanges();
            }
        }

        public override async Task UpdateAsync(Section resourceFromRequest, Section resourceFromDatabase, CancellationToken cancellationToken)
        {
            if (resourceFromDatabase.PublishTo != resourceFromRequest.PublishTo)
            {
                await PublishSection(resourceFromRequest, resourceFromRequest.Published);
            }
            if (resourceFromRequest.TitleMediafileId != null && PublishToAkuo(resourceFromRequest.PublishTo)) //always do titles and movements
                await MediafileRepository.Publish((int)resourceFromRequest.TitleMediafileId, "{'Public': 'true'}", true);
            await base.UpdateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
        }
    }
}
