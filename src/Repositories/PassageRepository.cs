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

        //get my passages in these projects
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Project> projects)
        {
            //var sections = entities.SelectMany(p => p.PassageSections.Select(ps => ps.Section));

            //this gets just the sections I have access to in these projects
            var sections = SectionRepository.UsersSections(SectionRepository.GetWithPassageSections(), projects);
            return UsersPassages(entities, sections);
        }

        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Section> sections = null)
        {
            if (sections == null)
            {
                sections = SectionRepository.GetWithPassageSections();
            }

            IEnumerable<int> passageIds = sections.SelectMany(s => s.PassageSections.Select(ps => ps.PassageId));

            //cast this to an ienumerable because to avoid error:A second operation started on this context before a previous operation completed. Any instance members are not guaranteed to be thread safe.
            entities = ((IEnumerable<Passage>)entities).Where(p => passageIds.Contains(p.Id)).AsQueryable();
            return entities;
        }

        public override IQueryable<Passage> Filter(IQueryable<Passage> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersPassages(entities, projects);
                }
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
 
        // This is the set of all Passages that a user has access to.
        public override IQueryable<Passage> Get()
        {
           return  UsersPassages(base.Get());
        }
    }
}