using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class OrgKeytermReferenceRepository : BaseRepository<Orgkeytermreference>
    {
        private readonly OrgKeytermRepository OrgKeytermRepository;

        public OrgKeytermReferenceRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrgKeytermRepository orgKeytermRepository
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
            OrgKeytermRepository = orgKeytermRepository;
        }

        public IQueryable<Orgkeytermreference> UsersOrgKeytermReferences(
            IQueryable<Orgkeytermreference> entities
        )
        {
            IQueryable<Orgkeyterm>? terms = OrgKeytermRepository.UsersOrgKeyterms(dbContext.Orgkeyterms.AsQueryable());
            return entities.Join(terms, o => o.OrgkeytermId, r => r.Id, (o, r) => o);
        }

        public IQueryable<Orgkeytermreference> ProjectOrgKeytermReferences(
            IQueryable<Orgkeytermreference> entities,
            string projectid
        )
        {
            IQueryable<Orgkeyterm>? terms = OrgKeytermRepository.ProjectOrgKeyterms(dbContext.Orgkeyterms.AsQueryable(), projectid);
            return entities.Join(terms, o => o.OrgkeytermId, r => r.Id, (o, r) => o);
        }

        #region Overrides
        public override IQueryable<Orgkeytermreference> FromProjectList(
            IQueryable<Orgkeytermreference>? entities,
            string idList
        )
        {
            return ProjectOrgKeytermReferences(entities ?? GetAll(), idList);
        }

        public override IQueryable<Orgkeytermreference> FromCurrentUser(
            IQueryable<Orgkeytermreference>? entities = null
        )
        {
            return UsersOrgKeytermReferences(entities ?? GetAll());
        }
        #endregion
    }
}
