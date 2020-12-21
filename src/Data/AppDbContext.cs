using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Paratext.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using JsonApiDotNetCore.Data;
using Microsoft.Extensions.Logging;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Repositories;
using JsonApiDotNetCore.Models;
using System.Collections.Generic;

namespace SIL.Transcriber.Data
{
    public class AppDbContext : BaseDbContext
    {
        public ICurrentUserContext CurrentUserContext { get; }
        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserContext currentUserContext, IHttpContextAccessor httpContextAccessor) : 
            base(options, httpContextAccessor)
        {
            CurrentUserContext = currentUserContext;
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

            passageEntity.HasMany(p => p.Mediafiles)
                .WithOne(mf => mf.Passage)
                .HasForeignKey(mf => mf.PassageId);

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
            DefineManyToMany(builder);
        }
        private int CurrentUserId()
        {
            string auth0Id = this.CurrentUserContext.Auth0Id;
            User userFromResult = Users.FirstOrDefault(u => u.ExternalId.Equals(auth0Id) && !u.Archived);
            return userFromResult == null ? -1 : userFromResult.Id;
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
            AddTimestamps(CurrentUserId());
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            UpdateSoftDeleteStatuses();
            AddTimestamps(CurrentUserId());
            return await base.SaveChangesAsync(cancellationToken);
        }
        //The database would handle this on delete, but EFCore would throw an error,
        //so handle it here too
        private void UpdateSoftDeleteStatuses()
        {
            List<EntityEntry> entries = ChangeTracker.Entries().ToList();
            for (int ix = entries.Count - 1; ix >= 0; ix--)
            {
                EntityEntry entry = entries[ix];
                if (entry.Entity is IArchive)
                {
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            entry.CurrentValues["Archived"] = false;
                            break;
                        case EntityState.Deleted:
                            if ((bool)entry.CurrentValues["Archived"] == true)
                                entry.State = EntityState.Detached;  
                            else
                            {
                                entry.State = EntityState.Modified;
                                entry.CurrentValues["Archived"] = true;
                            }
                            break;
                    }
                }
            }
        }
        public DbSet<Activitystate> Activitystates { get; set; }
        public DbSet<Dashboard> Dashboards { get; set; }
        public DbSet<DataChanges> DataChanges { get; set; }
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
    public class AppDbContextResolver : IDbContextResolver
    {
        private readonly AppDbContext _context;
        public AppDbContextResolver(AppDbContext context)
        {
            _context = context;
        }
        public DbContext GetContext()
        {
            return _context;
        }

        public DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
        {
            return _context.Set<TEntity>();
        }
    }

    public class AppDbContextRepository<TResource> : DefaultEntityRepository<TResource>
    where TResource : class, IIdentifiable<int>
    {
        public AppDbContextRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
           ) : base(loggerFactory, jsonApiContext, contextResolver)
        { }
    }
}
