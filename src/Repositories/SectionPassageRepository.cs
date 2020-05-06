
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Npgsql;
using PostgreSQLCopyHelper;
using SIL.Transcriber.Data;
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
        private void SetIds(NpgsqlConnection con, List<Section> sections)
        {
            string sql = "SELECT nextval('section_id_seq')";
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                foreach (Section s in sections)
                {
                    s.Id = (int)(long)cmd.ExecuteScalar();
                }
            }
        }
        private void SetIds(NpgsqlConnection con, List<Passage> passages)
        {
            string sql = "SELECT nextval('passages_id_seq')";
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                foreach (Passage p in passages)
                {
                     p.Id = (int)(long)cmd.ExecuteScalar();
                }
            }
        }
        public List<Section> BulkInsertSections(NpgsqlConnection connection, List<Section> sections)
        {
            SetIds(connection, sections);
            SetModifiedValues(sections, true);
            PostgreSQLCopyHelper<Section> copyHelper = new PostgreSQLCopyHelper<Section>("public", "sections")
                .MapInteger("id", x => x.Id)
                .MapText("name", x => x.Name)
                .MapInteger("planid", x => x.PlanId)
                .MapInteger("sequencenum", x => x.Sequencenum)
                .MapText("state", x => x.State)
                .MapTimeStamp("datecreated", x => x.DateCreated)
                .MapTimeStamp("dateupdated", x => x.DateUpdated)
                .MapBoolean("archived", x => x.Archived)
                .MapInteger("lastmodifiedby", x => x.LastModifiedBy)
                .MapText("lastmodifiedorigin", x => x.LastModifiedOrigin)
                .MapInteger("editorid", x => x.EditorId)
                .MapInteger("transcriberid", x => x.TranscriberId);

            Logger.LogInformation($"Insert Sections {sections.Count} {sections[0].PlanId}");
            //dbContext.BulkInsert(sections);
            copyHelper.SaveAll(connection, sections);
            return sections;
        }
        public List<Section> BulkUpdateSections(List<Section> sections)
        {
            dbContext.UpdateRange(sections);
            dbContext.SaveChanges();
            return sections;
            //SetModifiedValues(sections, false);
            //dbContext.BulkUpdate(sections);
            //return sections;
        }
        private static void DisplayStates(IEnumerable<EntityEntry> entries)
        {
            foreach (var entry in entries)
            {
                Console.WriteLine($"Entity: {entry.Entity.GetType().Name}, State: { entry.State.ToString()} ");
            }
        }
        public List<Passage> BulkInsertPassages(List<Passage> passages)
        {
            dbContext.UpdateRange(passages);
            DisplayStates(dbContext.ChangeTracker.Entries());
            dbContext.SaveChanges();
            return passages;
        }
        public List<Passage> BulkInsertPassages(NpgsqlConnection connection, List<Passage> passages)
        {
            SetIds(connection, passages);
            SetModifiedValues(passages, true);
            PostgreSQLCopyHelper<Passage> copyHelper = new PostgreSQLCopyHelper<Passage>("public", "sections")
               .MapInteger("id", x => x.Id)
               .MapText("title", x => x.Title)
               .MapText("book", x => x.Book)
               .MapText("reference", x => x.Reference)
               .MapInteger("sequencenum", x => x.Sequencenum)
               .MapText("state", x => x.State)
               .MapTimeStamp("datecreated", x => x.DateCreated)
               .MapTimeStamp("dateupdated", x => x.DateUpdated)
               .MapBoolean("archived", x => x.Archived)
               .MapInteger("lastmodifiedby", x => x.LastModifiedBy)
               .MapText("lastmodifiedorigin", x => x.LastModifiedOrigin);

            Logger.LogInformation($"Insert Passages {passages.Count} {passages[0]}");
            //dbContext.BulkInsert(passages);
            copyHelper.SaveAll(connection, passages);
            return passages;
        }
        public List<Passage> BulkUpdatePassages(List<Passage> passages)
        {
            //SetModifiedValues(passages, false);
            //dbContext.BulkUpdate(passages);
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
