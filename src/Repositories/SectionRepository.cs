using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class SectionRepository : BaseRepository<Section>
    {

        private ProjectRepository ProjectRepository;
        private PlanRepository PlanRepository;
        private AppDbContext AppDbContext;
        public SectionRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository,
            PlanRepository planRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
            PlanRepository = planRepository;
            AppDbContext = contextResolver.GetContext() as AppDbContext;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Project> projects)
        {
            var plans = PlanRepository.UsersPlans(AppDbContext.Plans, projects);
            return UsersSections(entities, plans);
        }

        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Plan> plans = null)
        {
            //this gets just the plans I have access to
            if (plans == null)
            {
                plans = PlanRepository.UsersPlans(dbContext.Plans);
            }

             return entities.Join(plans, s=>s.PlanId, p=>p.Id, (s, p) => s);
        }

        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassageSections()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passagesections");
            //no error...but no passagesections either  return Get().Include(s => s.PassageSections);
            var sections =  UsersSections(Include(dbContext.Sections, "passage-sections"));
            return sections;
        }

        #endregion
        #region Overrides
        public override IQueryable<Section> Filter(IQueryable<Section> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER)) 
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSections(entities, projects);
                }
                return UsersSections(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersSections(entities);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
        #region ParatextSync
        public IQueryable<Section> GetSectionsAtStatus(int projectId, string status)
        {
            return GetWithPassageSections().Where(s => s.Id == 721 || s.Id == 722);
            /*var sections = GetWithPassageSections().Include(s => s.Plan);
            var projectsections = projects.Join(sections, p => p.Id, s => s.Plan.ProjectId, (p, s) => s);
            return projectsections.Where(s => s.PassageSections.All(ps => ps.Passage.State == status));
            */
        }
        public async Task<IList<SectionSummary>> SectionSummary(int PlanId, string book, int chapter)
        {
            IList<SectionSummary> ss = new List<SectionSummary>();
            var passagesections = dbContext.Passagesections.Join(dbContext.Sections.Where(section => section.PlanId == PlanId), ps => ps.SectionId, section => section.Id, (ps, section) => new { ps.PassageId, section });
            var passages = dbContext.Passages.Join(passagesections, passage => passage.Id, ps => ps.PassageId, (passage, ps) => new { passage, ps.section }).Where(x => x.passage.Book == book && x.passage.StartChapter == chapter);
            await passages.GroupBy(p => p.section).ForEachAsync(ps =>
              {
                  var newss = new SectionSummary()
                  {
                      section = ps.FirstOrDefault().section,
                      Book = book,
                      startChapter = chapter,
                      endChapter = chapter,
                      startVerse = ps.Min(a => a.passage.StartVerse),
                      endVerse = ps.Max(a => a.passage.EndVerse),
                  };
                  ss.Add(newss);
              });
            return ss;
        }
        #endregion
        #region Assignments
        public IEnumerable<Passage> AssignUser(int Id, int UserId, string Rolename)
        {
            Role role = AppDbContext.Roles.Where(r => r.RoleNameString == Rolename).FirstOrDefault();
            if (role == null)
                throw new Exception("Invalid Role Requested" + Rolename);

            var passagesections = AppDbContext.Passagesections.Where(ps => ps.SectionId == Id).Include(ps => ps.Passage);
            UserPassage up;
            IEnumerable<Passage> passages = passagesections.Select(p => p.Passage);
            foreach (var ps in passagesections)
            {
                up = AppDbContext.Userpassages.Where(u => u.PassageId == ps.PassageId && u.RoleId == role.Id).FirstOrDefault();
                if (up == null)
                {
                    up = new UserPassage()
                    {
                        UserId = UserId,
                        PassageId = ps.PassageId,
                        RoleId = role.Id,
                    };
                    AppDbContext.Userpassages.Add(up);
                }
                else
                {
                    up.UserId = UserId;
                }
            }
            AppDbContext.SaveChanges();
            return passages;
        }
        public IEnumerable<Passage> DeleteAssignment(int Id, string Rolename)
        {
            Role role = AppDbContext.Roles.Where(r => r.RoleNameString == Rolename).FirstOrDefault();
            if (role == null)
                throw new Exception("Invalid Role Requested" + Rolename);

            var passagesections = AppDbContext.Passagesections.Where(ps => ps.SectionId == Id).Include(ps => ps.Passage);
            IEnumerable<Passage> passages = passagesections.Select(p => p.Passage);
            foreach (var ps in passagesections)
            {
                AppDbContext.Userpassages.RemoveRange(AppDbContext.Userpassages.Where(u => u.PassageId == ps.PassageId && u.RoleId == role.Id));
            }
            AppDbContext.SaveChanges();
            return passages;
        }
        public IEnumerable<Assignment> GetPassageAssignments(int id)
        {
            if (AppDbContext.Sections.Where(s => s.Id == id).FirstOrDefault() == null)
                throw new Exception("Invalid Section");

            var assignments = new List<Assignment>();
            PassageSection first = AppDbContext.Passagesections.Where(ps => ps.SectionId == id).FirstOrDefault();
            if (first == null)
                return assignments;

            var upQuery = from ups in AppDbContext.Userpassages
                                               where ups.PassageId == first.PassageId
                                               group ups by new { User = ups.User, Role = ups.Role } into UserRoleGroup
                                               select UserRoleGroup;

            foreach (var grp in upQuery)
            {
                assignments.Add(new Assignment()
                        {
                            User = grp.Key.User,
                            Role = grp.Key.Role
                        });
            }
            return assignments;
        }
        public IQueryable<Section> GetWithPassageAssignments(int id)
        {

            return base.Get().Where(u => u.Id == id)
                        .Include(u => u.PassageSections)
                            .ThenInclude(ps => ps.Passage)
                                .ThenInclude(p => p.UserPassages)
                                    .ThenInclude(up => up.User)
                        .Include(u => u.PassageSections)
                            .ThenInclude(ps => ps.Passage)
                                .ThenInclude(p => p.UserPassages)
                                   .ThenInclude(up => up.Role);
        }
        #endregion
    }
}