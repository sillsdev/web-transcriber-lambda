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
    public class PassageSectionRepository : BaseRepository<PassageSection>
    {

        private SectionRepository SectionRepository;

        public PassageSectionRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            SectionRepository = sectionRepository;
        }
       
        public IQueryable<PassageSection> UsersPassageSections(IQueryable<PassageSection> entities, IQueryable<Project> projects)
        {
            var sections = SectionRepository.UsersSections(dbContext.Sections, projects);

            return UsersPassageSections(entities, sections);
        }

        public IQueryable<PassageSection> UsersPassageSections(IQueryable<PassageSection> entities, IQueryable<Section> sections = null)
        {
            if (sections == null)
            {
                // sections = SectionRepository.GetWithPassageSections();
                //this is faster...
                sections = SectionRepository.UsersSections(dbContext.Sections);
            }
            return entities.Join(sections, ps => ps.SectionId, s => s.Id, (ps, s) => ps);
        }

        public override IQueryable<PassageSection> Filter(IQueryable<PassageSection> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                var projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                return UsersPassageSections(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersPassageSections(entities);
            }
            return base.Filter(entities, filterQuery); 
        }
        
    }
}