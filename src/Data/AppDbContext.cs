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
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SIL.Transcriber.Data
{
    public class AppDbContext : DbContext
    {
        public HttpContext HttpContext { get; }

        public ICurrentUserContext CurrentUserContext { get; }
        public DbContextOptions<AppDbContext> Options { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }
        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserContext currentUserContext, IHttpContextAccessor httpContextAccessor) : base(options)
        {
            HttpContext = httpContextAccessor.HttpContext;
            HttpContextAccessor = httpContextAccessor;
            CurrentUserContext = currentUserContext;
            Options = options;
        }
        private void LowerCaseDB(ModelBuilder builder)
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
        private void DefineManyToMany(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<Group> groupEntity = modelBuilder.Entity<Group>();
            EntityTypeBuilder<User> userEntity = modelBuilder.Entity<User>();
            EntityTypeBuilder<Role> roleEntity = modelBuilder.Entity<Role>();
            EntityTypeBuilder<Organization> orgEntity = modelBuilder.Entity<Organization>();
            EntityTypeBuilder<OrganizationMembership> orgMemberEntity = modelBuilder.Entity<OrganizationMembership>();
            EntityTypeBuilder<Project> projectEntity = modelBuilder.Entity<Project>();
            EntityTypeBuilder<GroupMembership> groupMemberEntity = modelBuilder.Entity<GroupMembership>();
            EntityTypeBuilder<Passage> passageEntity = modelBuilder.Entity<Passage>();
            EntityTypeBuilder<Section> sectionEntity = modelBuilder.Entity<Section>();

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
            string auth0Id = this.CurrentUserContext.Auth0Id;
            User userFromResult = Users.FirstOrDefault(u => u.ExternalId.Equals(auth0Id) && !u.Archived);
            return userFromResult == null ? -1 : userFromResult.Id;
        }
        //// https://benjii.me/2014/03/track-created-and-modified-fields-automatically-with-entity-framework-code-first/
        private void AddTimestamps()
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
            int userid = CurrentUserId();
            if (userid > 0) // we allow s3 trigger anonymous access
            {
                entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
                foreach (EntityEntry entry in entries)
                {
                    entry.CurrentValues["LastModifiedBy"] = userid;
                }

            }

            string origin = HttpContext.GetOrigin() ?? "http://localhost:3000";
            entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (EntityEntry entry in entries)
            {
                entry.CurrentValues["LastModifiedOrigin"] = origin;
            }
        }
        public async Task<int> SaveChangesNoTimestampAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            System.Collections.Generic.IEnumerable<EntityEntry> entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (EntityEntry entry in entries)
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
            foreach (EntityEntry entry in ChangeTracker.Entries())
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
        public DbSet<OrgData> Orgdatas { get; set; }
        public DbSet<OrganizationMembership> Organizationmemberships { get; set; }
        public DbSet<ParatextToken> Paratexttokens { get; set; }
        public DbSet<Passage> Passages { get; set; }
        public DbSet<PassageStateChange> Passagestatechanges { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<PlanType> Plantypes { get; set; }
        public DbSet<ProjData> Projdatas { get; set; }
        public DbSet<ProjectIntegration> Projectintegrations { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectType> Projecttypes { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<SectionPassage> Sectionpassages { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<VwPassageStateHistoryEmail> Vwpassagestatehistoryemails { get; set; }

    }
}
