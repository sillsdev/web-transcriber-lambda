﻿
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Repositories
{
    public class SectionPassageRepository : BaseRepository<SectionPassage>
    {
        protected IJsonApiContext JsonApiContext { get; }

        public SectionPassageRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            JsonApiContext = jsonApiContext;
        }
        public override async Task<SectionPassage> GetAsync(int id)
        {
            SectionPassage entity = await base.GetAsync(id); // dbContext.Sectionpassages.Where(e => e.Id == id).FirstOrDefault();
            if (entity != null && entity.Complete)
                return entity;
            return null;
        }
        public SectionPassage GetByUUID(Guid uuid)
        {
            return dbContext.Sectionpassages.Where(e => e.uuid == uuid).FirstOrDefault();
        }
        public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction BeginTransaction()
        {
            return dbContext.Database.BeginTransaction();
        }     
        private void SetModifiedValues(BaseModel m, int user, DateTime now, string origin, bool setCreated)
        {
            m.LastModifiedBy = user;
            if (setCreated) m.DateCreated = now;
            m.DateUpdated = now;
            m.LastModifiedOrigin = origin;
        }
        private void SetModifiedValues(List<Section> sections, bool setCreated)
        {
            int user = CurrentUser.Id;
            DateTime now = DateTime.UtcNow;
            string origin = dbContext.HttpContext.GetOrigin() ?? "http://localhost:3000";
            foreach (Section s in sections)
                SetModifiedValues(s, user, now, origin, setCreated);
        }
        private void SetModifiedValues(List<Passage> passages, bool setCreated)
        {
            int user = CurrentUser.Id;
            DateTime now = DateTime.UtcNow;
            string origin = dbContext.HttpContext.GetOrigin() ?? "http://localhost:3000";
            foreach (Passage p in passages)
                SetModifiedValues(p, user, now, origin, setCreated);
        }

        public List<Section> BulkUpdateSections(List<Section> sections)
        {
            dbContext.UpdateRange(sections);
            dbContext.SaveChanges();
            return sections;
        }

        public List<Passage> BulkUpdatePassages(List<Passage> passages)
        {
            dbContext.UpdateRange(passages);
            dbContext.SaveChanges();
            return passages;
        }

        public Passage GetPassage(int id)
        {
            return dbContext.Passages.First(p => p.Id == id);
        }
        public Section GetSection(int id)
        {
            return dbContext.Sections.First(p => p.Id == id);
        }
    }
}
