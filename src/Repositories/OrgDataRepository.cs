using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serialization;
using System.Collections.Immutable;

namespace SIL.Transcriber.Repositories
{
    public class OrgDataRepository : BaseRepository<Orgdata>
    {
        protected readonly OrganizationRepository organizationRepository;
        protected readonly GroupMembershipRepository gmRepository;
        protected readonly GroupRepository groupRepository;
        protected readonly IResourceGraph _resourceGraph;
        protected readonly IResourceFactory _resourceFactory;
        protected readonly IEnumerable<IQueryConstraintProvider> _constraintProviders;
        protected readonly IJsonApiOptions _options;
        protected readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;
        protected readonly IMetaBuilder _metaBuilder;

        public OrgDataRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository orgRepository,
            GroupMembershipRepository grpMemRepository,
            GroupRepository grpRepository,
            IMetaBuilder metaBuilder,
            IJsonApiOptions options
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
            organizationRepository = orgRepository;
            gmRepository = grpMemRepository;
            groupRepository = grpRepository;
            _resourceGraph = resourceGraph;
            _resourceFactory = resourceFactory;
            _constraintProviders = constraintProviders;
            _options = options;
            _resourceDefinitionAccessor = resourceDefinitionAccessor;
            _metaBuilder = metaBuilder;
        }

        private string ToJson<TResource>(IEnumerable<TResource> resources)
            where TResource : class, IIdentifiable
        {
            return SerializerHelpers.ResourceListToJson<TResource>(
                resources,
                ResourceGraph,
                _options,
                _resourceDefinitionAccessor,
                _metaBuilder
            );
        }

        private IQueryable<Orgdata> GetData(
            IQueryable<Orgdata> entities,
            string start,
            int version = 1
        )
        {
            Orgdata orgData = entities.First();
            string data = "";
            if (!int.TryParse(start, out int iStart))
                iStart = 0;

            int iStartNext = iStart;
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            do
            {
                if (
                    !CheckAdd(
                        0,
                        ToJson(dbContext.Activitystates),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;
                if (
                    !CheckAdd(
                        1,
                        ToJson(dbContext.Projecttypes),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;
                if (
                    !CheckAdd(
                        2,
                        ToJson(dbContext.Plantypes),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;
                if (!CheckAdd(3, ToJson(dbContext.Roles), dtBail, ref iStartNext, ref data))
                    break;
                if (
                    !CheckAdd(
                        4,
                        ToJson(dbContext.Integrations),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;

                IQueryable<Organization> orgs = organizationRepository.GetMine(); //this limits to current user

                if (!CheckAdd(6, ToJson(orgs), dtBail, ref iStartNext, ref data))
                    break;
                IQueryable<Group> groups = groupRepository.GetMine();
                IQueryable<Groupmembership> gms = gmRepository.GetMine(groups);
                if (!CheckAdd(7, ToJson(gms), dtBail, ref iStartNext, ref data))
                    break;
                //groups
                if (!CheckAdd(8, ToJson(groups), dtBail, ref iStartNext, ref data))
                    break;
                //invitations
                if (
                    !CheckAdd(
                        9,
                        ToJson(dbContext.InvitationsData.Join(
                                orgs,
                                i => i.OrganizationId,
                                o => o.Id,
                                (i, o) => i
                            )
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;


                //orgmems
                IQueryable<Organizationmembership> oms = dbContext.OrganizationmembershipsData
                    .Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om)
                    .Where(x => !x.Archived);
                if (!CheckAdd(10, ToJson(oms), dtBail, ref iStartNext, ref data))
                    break;
                int userid = CurrentUser?.Id ?? -1;
                //users
                if (
                    !CheckAdd(
                        11,
                        ToJson(
                            oms.Any()
                                ? dbContext.Users
                                    .Join(oms, u => u.Id, om => om.UserId, (u, om) => u)
                                    .Where(x => !x.Archived)
                                : dbContext.Users.Where(x => x.Id == userid)
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;

                //projects
                IEnumerable<Project> projects = dbContext.ProjectsData
                    .Join(gms, p => p.GroupId, gm => gm.GroupId, (p, gm) => p)
                    .Where(x => !x.Archived);
                if (!CheckAdd(12, ToJson(projects), dtBail, ref iStartNext, ref data))
                    break;
                //projectintegrations
                if (
                    !CheckAdd(
                        13,
                        ToJson(
                            dbContext.ProjectintegrationsData
                                .Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl)
                                .Where(x => !x.Archived)
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;
                //plans
                if (
                    !CheckAdd(
                        14,
                        ToJson(
                            dbContext.PlansData
                                .Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl)
                                .Where(x => !x.Archived)
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;

                if (version > 3)
                {
                    if (
                        !CheckAdd(
                            15,
                            ToJson(dbContext.Workflowsteps.Where(x => !x.Archived)),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;
                    IEnumerable<int> ids = orgs.Select(o => o.Id);
                    IQueryable<Artifactcategory> cats = dbContext.ArtifactcategoriesData
                        .Where(c =>
                            (c.OrganizationId == null || ids.Contains((int)c.OrganizationId))
                            && !c.Archived
                    );
                    if (
                        !CheckAdd(
                            16,
                            ToJson(cats),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;
                    IQueryable<Artifacttype> typs = dbContext.ArtifacttypesData
                        .Include(c => c.Organization)
                        .Where(c =>
                                (c.OrganizationId == null || ids.Contains((int)c.OrganizationId))
                                && !c.Archived
                        );
                    if (!CheckAdd(17, ToJson(typs), dtBail, ref iStartNext, ref data))
                        break;
                    if (
                        !CheckAdd(
                            18,
                            ToJson(
                                dbContext.OrgworkflowstepsData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived)
                            ),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            orgData.Json = data + FinishData();
            orgData.StartIndex = iStartNext;
            return entities;
        }

        protected override IQueryable<Orgdata> GetAll()
        {
            List<Orgdata> entities = new() { new Orgdata() };
            return entities.AsAsyncQueryable();
        }

        public override IQueryable<Orgdata> FromCurrentUser(IQueryable<Orgdata>? entities = null)
        {
            return GetAll();
        }

        protected override IQueryable<Orgdata> FromStartIndex(
            IQueryable<Orgdata>? entities,
            string startIndex,
            string version = "",
            string projectid = ""
        )
        {
            dynamic? x = JsonConvert.DeserializeObject(version);
            int filterVersion = x?.version ?? 1;

            IQueryable<Orgdata> result = GetData(
                entities ?? GetAll(),
                startIndex,
                filterVersion
            );
            return result;
        }

        public override IQueryable<Orgdata> FromProjectList(
            IQueryable<Orgdata>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
