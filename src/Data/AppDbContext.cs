using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIL.Paratext.Models;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Data.DbContextExtentions;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace SIL.Transcriber.Data
{
    public class AppDbContext : DbContext
    {
        private int _currentUser = -1;
        public ICurrentUserContext CurrentUserContext { get; }
        public ILogger Logger;
        public HttpContext? HttpContext { get; }
        public DbContextOptions Options { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }

        #region DBSet
        public DbSet<Activitystate> Activitystates => Set<Activitystate>(); 
        public DbSet<Artifactcategory> Artifactcategorys => Set<Artifactcategory>();
        public DbSet<Artifacttype> Artifacttypes => Set<Artifacttype>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<CurrentVersion> CurrentVersions => Set<CurrentVersion>();
        public DbSet<Dashboard> Dashboards => Set<Dashboard>();
        public DbSet<DataChanges> DataChanges => Set<DataChanges>();
        public DbSet<Discussion> Discussions => Set<Discussion>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMembership> Groupmemberships => Set<GroupMembership>();
        public DbSet<Integration> Integrations => Set<Integration>();
        public DbSet<Invitation> Invitations => Set<Invitation>();
        public DbSet<Mediafile> Mediafiles => Set<Mediafile>();
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<OrganizationMembership> Organizationmemberships => Set<OrganizationMembership>();
        public DbSet<Orgdata> Orgdatas => Set<Orgdata>();
        public DbSet<OrgWorkflowstep> Orgworkflowsteps => Set<OrgWorkflowstep>();
        public DbSet<ParatextToken> Paratexttokens => Set<ParatextToken>();
        public DbSet<Passage> Passages => Set<Passage>();
        public DbSet<PassageStateChange> Passagestatechanges => Set<PassageStateChange>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<PlanType> Plantypes => Set<PlanType>();
        public DbSet<ProjData> Projdatas => Set<ProjData>();
        public DbSet<ProjectIntegration> Projectintegrations => Set<ProjectIntegration>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectType> Projecttypes => Set<ProjectType>();
        public DbSet<Resource> Resources => Set<Resource>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Section> Sections => Set<Section>();
         public DbSet<SectionPassage> Sectionpassages => Set<SectionPassage>();
        public DbSet<SectionResource> Sectionresources => Set<SectionResource>();
        public DbSet<SectionResourceUser> Sectionresourceusers => Set<SectionResourceUser>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserVersion> UserVersions => Set<UserVersion>();
        public DbSet<VwPassageStateHistoryEmail> Vwpassagestatehistoryemails => Set<VwPassageStateHistoryEmail>();
        public DbSet<Workflowstep> Workflowsteps => Set<Workflowstep>();
        #endregion

        public AppDbContext(DbContextOptions<AppDbContext> options,
                            ICurrentUserContext currentUserContext,
                            IHttpContextAccessor httpContextAccessor,
                            ILoggerFactory loggerFactory)
            : base(options)
        {
            CurrentUserContext = currentUserContext;
            Logger = new Logger<AppDbContext>(loggerFactory);
            HttpContext = httpContextAccessor.HttpContext;
            HttpContextAccessor = httpContextAccessor;
            Options = options;
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasPostgresExtension("uuid-ossp");
            //make all query items lowercase to send to postgres...
            LowerCaseDB(builder);

            DefineManyToMany(builder);
            DefineLastModifiedByUser(builder);
        }
        private static void DefineLastModifiedByUser(ModelBuilder builder)
        {
            builder.Entity<Activitystate>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Artifactcategory>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Artifacttype>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Comment>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<CurrentVersion>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<DataChanges>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Discussion>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Group>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<GroupMembership>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Integration>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Invitation>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Mediafile>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Mediafile>()
                .HasOne(o => o.Passage)
                .WithMany()
                .HasForeignKey(o => o.PassageId);
            builder.Entity<Mediafile>()
                .HasOne(o => o.ResourcePassage)
                .WithMany()
                .HasForeignKey(o => o.ResourcePassageId);
            builder.Entity<Organization>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Orgdata>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<OrganizationMembership>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<OrgWorkflowstep>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Passage>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<PassageStateChange>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Plan>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<PlanType>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<ProjData>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Project>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<ProjectIntegration>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<ProjectType>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);   
            builder.Entity<Role>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Section>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<SectionPassage>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<SectionResource>()
               .HasOne(o => o.LastModifiedByUser)
               .WithMany()
               .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<SectionResourceUser>()
               .HasOne(o => o.LastModifiedByUser)
               .WithMany()
               .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<User>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<UserVersion>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            builder.Entity<Workflowstep>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
        }

        private static void DefineManyToMany(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GroupMembership>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);

            modelBuilder.Entity<Group>()
                .HasMany(g => g.GroupMemberships)
                .WithOne(gm => gm.Group)
                .HasForeignKey(gm => gm.GroupId);
            
            EntityTypeBuilder<User> userEntity = modelBuilder.Entity<User>();
            userEntity
                .HasMany(u => u.OrganizationMemberships)
                .WithOne(om => om.User)
                .HasForeignKey(om => om.UserId);
            userEntity
                 .HasMany(u => u.GroupMemberships)
                .WithOne(gm => gm.User)
                .HasForeignKey(gm => gm.UserId);
            userEntity
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);

            EntityTypeBuilder<Organization> orgEntity = modelBuilder.Entity<Organization>();
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
                .HasDefaultValue(false);
            orgEntity
                .HasOne(o => o.Owner)
                .WithMany()
                .HasForeignKey(o => o.OwnerId);

            modelBuilder.Entity<Section>().HasOne(s => s.Plan)
                .WithMany(p => p.Sections)
                .HasForeignKey(sectionEntity => sectionEntity.PlanId);

            modelBuilder.Entity<Project>()
                .Property(p => p.IsPublic)
                .HasDefaultValue(false);

            modelBuilder.Entity<OrgWorkflowstep>()
                .HasOne(s => s.Parent)
                .WithMany()
                .HasForeignKey(s => s.ParentId);
             modelBuilder.Entity<SectionResource>()
                .HasMany(r => r.SectionResourceUsers)
                .WithOne(srow => srow.SectionResource)
                .HasForeignKey(x => x.SectionResourceId);
        }

        public async Task<int> SaveChangesNoTimestampAsync(CancellationToken cancellationToken = default)
        {
            IEnumerable<EntityEntry> entries = ChangeTracker.Entries().Where(e => e.Entity is ILastModified && (e.State == EntityState.Added || e.State == EntityState.Modified));
            foreach (EntityEntry entry in entries)
            {
                entry.CurrentValues["LastModifiedOrigin"] = "electron";
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
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
                            if ((bool)(entry.CurrentValues["Archived"]??false) == true)
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
        private int CurrentUserId()
        {
            if (_currentUser < 0)
            {
                string auth0Id = CurrentUserContext.Auth0Id;
                User? userFromResult = Users.FirstOrDefault(u => (u.ExternalId??"").Equals(auth0Id) && !u.Archived);
                _currentUser = userFromResult == null ? -1 : userFromResult.Id;
            }
            return _currentUser;
        }
        public override int SaveChanges()
        {
            UpdateSoftDeleteStatuses();
            AddTimestamps(this, HttpContext, CurrentUserId());
            return base.SaveChanges();
        }
        public string Fingerprint()
        {
            return GetFingerprint(HttpContext);
        }
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

    public class AppDbContextRepository<TResource> : EntityFrameworkCoreRepository<TResource, int>
    where TResource : class, IIdentifiable<int>
    {
        public AppDbContextRepository(
             ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders, ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor
           ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
               loggerFactory, resourceDefinitionAccessor)
        {
        }
    }
}
