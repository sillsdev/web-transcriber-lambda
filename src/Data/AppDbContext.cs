using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Paratext.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Data
{
    public class AppDbContext : DbContext
    {
        public HttpContext HttpContext { get; }

        protected ICurrentUserContext CurrentUserContext;
        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserContext currentUserContext, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            HttpContext = httpContextAccessor.HttpContext;
            CurrentUserContext = currentUserContext;
        }

        private void LowerCaseDB(ModelBuilder builder)
        {
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                // Replace table names
                entity.Relational().TableName = entity.Relational().TableName.ToLower();

                // Replace column names            
                foreach (var property in entity.GetProperties())
                {
                    property.Relational().ColumnName = property.Name.ToLower();
                }

                foreach (var key in entity.GetKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToLower();
                }

                foreach (var key in entity.GetForeignKeys())
                {
                    key.Relational().Name = key.Relational().Name.ToLower();
                }

                foreach (var index in entity.GetIndexes())
                {
                    index.Relational().Name = index.Relational().Name.ToLower();
                }
            }
        }
        private void DefineManyToMany(ModelBuilder modelBuilder)
        {
            var groupEntity = modelBuilder.Entity<Group>();
            var userEntity = modelBuilder.Entity<User>();
            var roleEntity = modelBuilder.Entity<Role>();
            var orgEntity = modelBuilder.Entity<Organization>();
            var orgMemberEntity = modelBuilder.Entity<OrganizationMembership>();
            var projectEntity = modelBuilder.Entity<Project>();
            var groupMemberEntity = modelBuilder.Entity<GroupMembership>();
            var passageEntity = modelBuilder.Entity<Passage>();
            var sectionEntity = modelBuilder.Entity<Section>();


            groupEntity
                .HasMany(g => g.GroupMemberships)
                .WithOne(gm => gm.Group)
                .HasForeignKey(gm => gm.GroupId);

            userEntity
                .HasMany(u => u.OrganizationMemberships)
                .WithOne(om => om.User)
                .HasForeignKey(om => om.UserId);

            userEntity
                .HasMany(u => u.GroupMemberships)
                .WithOne(gm => gm.User)
                .HasForeignKey(gm => gm.UserId);


            orgEntity
                .HasMany(o => o.OrganizationMemberships)
                .WithOne(om => om.Organization)
                .HasForeignKey(om => om.OrganizationId);

            orgEntity
                .HasMany(o => o.Groups)
                .WithOne(g => g.Owner)
                .HasForeignKey(g => g.OwnerId);

            orgEntity
                .HasMany(o => o.Projects)
                .WithOne(p => p.Organization)
                .HasForeignKey(p => p.OrganizationId);

            orgEntity
                .Property(o => o.PublicByDefault)
                .HasDefaultValue(true);

            projectEntity
                .Property(p => p.IsPublic)
                .HasDefaultValue(true);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasPostgresExtension("uuid-ossp");
            //make all query items lowercase to send to postgres...
            LowerCaseDB(builder);
            DefineManyToMany(builder);
        }
        private int CurrentUserId()
        { 
             var auth0Id = this.CurrentUserContext.Auth0Id; 
             var userFromResult = Users.FirstOrDefault(u => u.ExternalId.Equals(auth0Id) && !u.Archived);
             return userFromResult == null ? -1 : userFromResult.Id;
        }
        //// https://benjii.me/2014/03/track-created-and-modified-fields-automatically-with-entity-framework-code-first/
        private void AddTimestamps()
        {
            var entries = ChangeTracker.Entries().Where(e => e.Entity is ITrackDate && (e.State == EntityState.Added || e.State == EntityState.Modified));
            DateTime now = DateTime.UtcNow;
            foreach (var entry in entries)
            {
                if (entry.Entity is ITrackDate trackDate)
                {
                    if (entry.State == EntityState.Added)
                    {
                        trackDate.DateCreated = now;
                    }
                    trackDate.DateUpdated = now;
                }
            }
            var userid = CurrentUserId();
            if (userid > 0) // we allow s3 trigger anonymous access
            {
                entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
                foreach (var entry in entries)
                {
                    entry.CurrentValues["LastModifiedBy"] = userid;
                }
            }

            var origin = HttpContext.GetOrigin() ?? "anonymous";
            entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (var entry in entries)
            {
                entry.CurrentValues["LastModifiedOrigin"] = origin;
            }
        }
        public async Task<int> SaveChangesNoTimestampAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (var entry in entries)
            {
                entry.CurrentValues["LastModifiedOrigin"] = "electron";
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
        public override int SaveChanges()
        {
            UpdateSoftDeleteStatuses();
            AddTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            UpdateSoftDeleteStatuses();
            AddTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }
        //The database would handle this on delete, but EFCore would throw an error,
        //so handle it here too
        private void UpdateSoftDeleteStatuses()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is IArchive)
                {
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            entry.CurrentValues["Archived"] = false;
                            break;
                        case EntityState.Deleted:
                            entry.State = EntityState.Modified;
                            entry.CurrentValues["Archived"] = true;
                            break;
                    }
                }
            }
        }
        public DbSet<Activitystate> Activitystates { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMembership> Groupmemberships { get; set; }
        public DbSet<Integration> Integrations { get; set; }
        public DbSet<Invitation> Invitations { get; set; }
        public DbSet<Mediafile> Mediafiles { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationMembership> Organizationmemberships { get; set; }
        public DbSet<ParatextToken> Paratexttokens { get; set; }
        public DbSet<Passage> Passages { get; set; }
        public DbSet<PassageStateChange> Passagestatechanges { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<PlanType> Plantypes { get; set; }
        public DbSet<ProjectIntegration> Projectintegrations { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectType> Projecttypes { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<VwPassageStateHistoryEmail> Vwpassagestatehistoryemails { get; set; }

    }
}
