using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System;
using System.Linq;

namespace SIL.Transcriber.Data
{
    public class BaseDbContext : DbContext
    {
        public HttpContext HttpContext { get; }
        public DbContextOptions Options { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }
        public BaseDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            HttpContext = httpContextAccessor.HttpContext;
            HttpContextAccessor = httpContextAccessor;
            Options = options;
        }
        protected void LowerCaseDB(ModelBuilder builder)
        {
            foreach (IMutableEntityType entity in builder.Model.GetEntityTypes())
            {
                // Replace table names
                entity.Relational().TableName = entity.Relational().TableName.ToLower();

                // Replace column names            
                foreach (IMutableProperty property in entity.GetProperties())
                {
                    property.Relational().ColumnName = property.Name.ToLower();
                }

                foreach (IMutableKey key in entity.GetKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToLower();
                }

                foreach (IMutableForeignKey key in entity.GetForeignKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToLower();
                }

                foreach (IMutableIndex index in entity.GetIndexes())
                {
                    index.Relational().Name = index.Relational().Name.ToLower();
                }
            }
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasPostgresExtension("uuid-ossp");
            //make all query items lowercase to send to postgres...
            LowerCaseDB(builder);
        }
        //// https://benjii.me/2014/03/track-created-and-modified-fields-automatically-with-entity-framework-code-first/
        protected void AddTimestamps(int userid)
        {
            System.Collections.Generic.IEnumerable<EntityEntry> entries = ChangeTracker.Entries().Where(e => e.Entity is ITrackDate && (e.State == EntityState.Added || e.State == EntityState.Modified));
            DateTime now = DateTime.UtcNow;
            foreach (EntityEntry entry in entries)
            {
                if (entry.Entity is ITrackDate trackDate)
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (trackDate.DateCreated == null) //if the front end set it, leave it.  We're using this to catch duplicates
                        {
                            trackDate.DateCreated = now;
                            trackDate.DateUpdated = now;
                        }
                    }
                    else
                        trackDate.DateUpdated = now;
                }
            }
            if (userid > 0) // we allow s3 trigger anonymous access
            {
                entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
                foreach (EntityEntry entry in entries)
                {
                    entry.CurrentValues["LastModifiedBy"] = userid;
                }

            }
            string origin = HttpContext.GetFP() ?? "noFP";
            entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (EntityEntry entry in entries)
            {
                entry.CurrentValues["LastModifiedOrigin"] = origin;
            }
        }

    }

}
