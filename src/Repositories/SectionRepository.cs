using System;
using System.Collections.Generic;
using System.Linq;
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
            //var plans = PlanRepository.UsersPlans(PlanRepository.Get(), projects);
            var plans = PlanRepository.UsersPlans(AppDbContext.Plans, projects);
            return UsersSections(entities, plans);

        }
        private IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Plan> plans = null)
        {
            //this gets just the plans I have access to
            if (plans == null)
            {
                plans = PlanRepository.Get();
            }

            IEnumerable<int> planIds = plans.Select(p => p.Id);

            //cast this to an ienumerable to avoid an error:A second operation started on this context before a previous operation completed. Any instance members are not guaranteed to be thread safe.
            //something in here is secretly async...but I can't find it
            return ((IEnumerable<Section>)entities).Where(s => planIds.Contains(s.PlanId)).AsQueryable();
        }
        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassageSections()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passagesections");
            //no error...but no passagesections either  return Get().Include(s => s.PassageSections);
            System.Diagnostics.Debug.WriteLine("Getting sections with ps" + DateTime.Now.ToLongTimeString() );
            var sections =  UsersSections(Include(base.Get(), "passage-sections" ));
            System.Diagnostics.Debug.WriteLine("done Getting sections with ps" + DateTime.Now.ToLongTimeString());
            return sections;
        }

        // This is the set of all Sections that a user has access to.
        public override IQueryable<Section> Get()
        {
            return UsersSections(base.Get());
        }
        #endregion
        #region Overrides
        public override IQueryable<Section> Filter(IQueryable<Section> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER)) //unless there is a specific org, we've handled orgs in the Get
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSections(entities, projects);
                }
                return entities;
            }
            return base.Filter(entities, filterQuery);
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