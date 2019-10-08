using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class PassageRepository : BaseRepository<Passage>
    {

        private ProjectRepository ProjectRepository;
        private SectionRepository SectionRepository;

        public PassageRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository,
            SectionRepository sectionRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
            SectionRepository = sectionRepository;
        }
       
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Project> projects)
        {
            var sections = SectionRepository.UsersSections(dbContext.Sections, projects);

            return UsersPassages(entities, sections);
        }

        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Section> sections = null)
        {
            if (sections == null)
            {
                // sections = SectionRepository.GetWithPassageSections();
                //this is faster...
                sections = SectionRepository.UsersSections(dbContext.Sections);
            }
            var passagesections = dbContext.Passagesections.Join(sections, ps => ps.SectionId, s => s.Id, (ps, s) => ps);

            return entities.Join(passagesections, p => p.Id, ps => ps.PassageId, (p, ps) => p);
        }

        public async System.Threading.Tasks.Task<IQueryable<Passage>> ReadyToSyncAsync(int PlanId)
        {
            var passagesections = dbContext.Passagesections.Join(dbContext.Sections.Where(s => s.PlanId == PlanId), ps => ps.SectionId, s => s.Id, (ps, s)  => new { ps.PassageId, s.Sequencenum });
            var passages = dbContext.Passages.Join(passagesections, p => p.Id, ps => ps.PassageId, (p, ps) => p).Where(p => p.ReadyToSync);

            return passages;
        }
        public override IQueryable<Passage> Filter(IQueryable<Passage> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                return UsersPassages(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersPassages(entities);
            }
            return base.Filter(entities, filterQuery); 
        }
        
    }
}