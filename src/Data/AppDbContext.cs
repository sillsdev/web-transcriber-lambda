

using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIL.Transcriber.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        { }

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

            var userEntity = modelBuilder.Entity<User>();
            var roleEntity = modelBuilder.Entity<Role>();
            var userRoleEntity = modelBuilder.Entity<UserRole>();
            var orgEntity = modelBuilder.Entity<Organization>();
            var orgMemberEntity = modelBuilder.Entity<OrganizationMembership>();
            var projectEntity = modelBuilder.Entity<Project>();
            var groupMemberEntity = modelBuilder.Entity<GroupMembership>();
            var passageEntity = modelBuilder.Entity<Passage>();
            var sectionEntity = modelBuilder.Entity<Section>();

            passageEntity
                .HasMany(p => p.PassageSections)
                .WithOne(ps => ps.Passage)
                .HasForeignKey(ps => ps.PassageId);

            sectionEntity
                .HasMany(s => s.PassageSections)
                .WithOne(ps => ps.Section)
                .HasForeignKey(ps => ps.SectionId);

            userEntity
                .HasMany(u => u.OrganizationMemberships)
                .WithOne(om => om.User)
                .HasForeignKey(om => om.UserId);

            userEntity
                .HasMany(u => u.GroupMemberships)
                .WithOne(gm => gm.User)
                .HasForeignKey(gm => gm.UserId);

            userEntity
                .HasMany(u => u.UserRoles)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId);

            roleEntity
                .HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId);

            orgEntity
                .HasMany(o => o.OrganizationMemberships)
                .WithOne(om => om.Organization)
                .HasForeignKey(om => om.OrganizationId);

            orgEntity
                .HasMany(o => o.UserRoles)
                .WithOne(r => r.Organization)
                .HasForeignKey(r => r.OrganizationId);

            orgEntity
                .HasMany(o => o.Groups)
                .WithOne(g => g.Owner)
                .HasForeignKey(g => g.OwnerId);

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
        }
        public override int SaveChanges()
        {
            AddTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            AddTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        public DbSet<ActivityState> ActivityStates { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMembership> Groupmemberships { get; set; }
        public DbSet<Integration> Integrations { get; set; }
        public DbSet<Mediafile> Mediafiles { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationMembership> Organizationmemberships { get; set; }
        public DbSet<Passage> Passages { get; set; }
        public DbSet<Passagesection> Passagesections { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<PlanType> Plantypes { get; set; }
        public DbSet<ProjectIntegration> Projectintegrations { get; set; }
        public DbSet<Project> Projects { get; set; }
        public  DbSet<ProjectType> Projecttypes { get; set; }
        public  DbSet<ProjectUser> Projectusers { get; set; }
        public  DbSet<Reviewer> Reviewers { get; set; }
        public  DbSet<Role> Roles { get; set; }
        public  DbSet<Section> Sections { get; set; }
        public DbSet<UserRole> Userroles { get; set; }
        public DbSet<UserPassage> Userpassages { get; set; }
        public  DbSet<User> Users { get; set; }

    }
}
