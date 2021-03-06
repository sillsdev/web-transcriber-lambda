﻿using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class PassageRepository : BaseRepository<Passage>
    {

        private SectionRepository SectionRepository;

        public PassageRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            SectionRepository = sectionRepository;
        }
       
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Project> projects)
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, projects);
            return SectionsPassages(entities, sections);
        }

        public IQueryable<Passage> SectionsPassages(IQueryable<Passage> entities, IQueryable<Section> sections)
        {
            return entities.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p);
        }
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Section> sections = null)
        {
            if (sections == null)
            {
                sections = SectionRepository.UsersSections(dbContext.Sections);
            }
            return SectionsPassages(entities, sections);
        }
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, int planid)
        {
            IQueryable<Plan> plans = dbContext.Plans.Where(p => p.Id == planid);
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, plans);
            return SectionsPassages(entities, sections);
        }
        public IQueryable<Passage> ProjectPassages(IQueryable<Passage> entities, string projectid)
        {
            IQueryable<Section> sections = SectionRepository.ProjectSections(dbContext.Sections, projectid);
            return SectionsPassages(entities, sections);
        }
        public IQueryable<Passage> ReadyToSync(int PlanId)
        {
            IQueryable<Section> sections = dbContext.Sections.Where(s => s.PlanId == PlanId);
            IQueryable<Passage> passages = dbContext.Passages.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p).Where(p => p.ReadyToSync).Include(p => p.Section);
            return passages;
        }
        public int ProjectId(Passage passage)
        {
            return dbContext.Sections.Where(s => s.Id == passage.SectionId).Join(dbContext.Plans, s => s.PlanId, p => p.Id, (s, p) => p).FirstOrDefault().ProjectId ;
        }
        public override IQueryable<Passage> Filter(IQueryable<Passage> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                IQueryable<Project> projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                return UsersPassages(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersPassages(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectPassages(entities, filterQuery.Value);
            }
            if (filterQuery.Has(PLANID))
            {
                if (int.TryParse(filterQuery.Value, out int plan))
                    return UsersPassages(entities, plan);
            }
            return base.Filter(entities, filterQuery); 
        }
        
    }
}