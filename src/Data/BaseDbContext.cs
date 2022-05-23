using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions;

namespace SIL.Transcriber.Data
{
    public static class DbContextExtentions
    {
        public static void LowerCaseDB(ModelBuilder builder)
        {
            foreach (IMutableEntityType entity in builder.Model.GetEntityTypes())
            {
#pragma warning disable EF1001 // Internal EF Core API usage.
                if (entity is EntityType { IsImplicitlyCreatedJoinEntityType: true })
#pragma warning restore EF1001 // Internal EF Core API usage.
                {
                    continue;
                }
                // Replace table names
                //05/16/22 This doesn't work anymore so use the [Table] schema notation--and now it does...
                entity.SetTableName(entity.GetTableName()?.ToLower());
                Console.Write("tablename:{0} {1} {2}", entity.GetTableName(), entity.GetDefaultTableName(), entity.DisplayName());

                // Replace column names            
                foreach (IMutableProperty property in entity.GetProperties())
                {
                    property.SetColumnName(property.Name.ToLower());
                }

                foreach (IMutableKey key in entity.GetKeys())
                {
                    key.SetName(key.GetName() ?? "".ToLower());
                }

                foreach (IMutableForeignKey key in entity.GetForeignKeys())
                {
                    key.SetConstraintName(key.GetConstraintName() ?? "".ToLower());
                }

                foreach (IMutableIndex index in entity.GetIndexes())
                {
                    index.SetDatabaseName(index.GetDatabaseName().ToLower());
                }
            }
        }

        public static string GetFingerprint(HttpContext? http)
        {
            return http?.GetFP() ?? "noFP";
        }
        //// https://benjii.me/2014/03/track-created-and-modified-fields-automatically-with-entity-framework-code-first/
        public static void AddTimestamps(DbContext dbc, HttpContext? http, int userid)
        {
            System.Collections.Generic.IEnumerable<EntityEntry> entries = dbc.ChangeTracker.Entries().Where(e => e.Entity is ITrackDate && (e.State == EntityState.Added || e.State == EntityState.Modified));
            DateTime now = DateTime.UtcNow.AddSeconds(2).SetKindUtc();

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
                    {
                        trackDate.DateCreated = trackDate.DateCreated.SetKindUtc();
                        trackDate.DateUpdated = now;
                    }
                }
            }
            if (userid > 0) // we allow s3 trigger anonymous access
            {
                entries = dbc.ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
                foreach (EntityEntry entry in entries)
                {
                    entry.CurrentValues["LastModifiedBy"] = userid;
                }

            }
            string origin = GetFingerprint(http);
            entries = dbc.ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (EntityEntry entry in entries)
            {
                entry.CurrentValues["LastModifiedOrigin"] = origin;
            }
        }

    }

}
