﻿using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class PassageStateChangeRepository : BaseRepository<Passagestatechange>
    {

        readonly private SectionRepository SectionRepository;

        public PassageStateChangeRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            SectionRepository = sectionRepository;
        }

        public IQueryable<Passagestatechange> SectionsPassageStateChanges(IQueryable<Passagestatechange> entities, IQueryable<Section> sections)
        {
            return sections.Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Join(entities, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
        }
        public IQueryable<Passagestatechange> UsersPassageStateChanges(IQueryable<Passagestatechange> entities, IQueryable<Project> projects)
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, projects);
            return SectionsPassageStateChanges(entities, sections);
        }
        public IQueryable<Passagestatechange> UsersPassageStateChanges(IQueryable<Passagestatechange> entities, IQueryable<Section>? sections = null)
        {
            if (sections == null)
                sections = SectionRepository.UsersSections(dbContext.Sections);
            return SectionsPassageStateChanges(entities, sections);
        }
        public IQueryable<Passagestatechange> ProjectPassageStateChanges(IQueryable<Passagestatechange> entities, string projectid)
        {
            IQueryable<Section> sections = SectionRepository.ProjectSections(dbContext.Sections, projectid);
            return SectionsPassageStateChanges(entities, sections);
        }

        public override IQueryable<Passagestatechange> FromCurrentUser(IQueryable<Passagestatechange>? entities = null)
        {
            return UsersPassageStateChanges(entities ?? GetAll());
        }
        protected override IQueryable<Passagestatechange> FromProjectList(IQueryable<Passagestatechange>? entities, string idList)
        {
            return ProjectPassageStateChanges(entities ?? GetAll(), idList);
        }
    }
}