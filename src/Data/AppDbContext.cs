using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIL.Paratext.Models;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility.Extensions;
using static SIL.Transcriber.Data.DbContextExtentions;

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
        public DbSet<Currentversion> Currentversions => Set<Currentversion>();
        public DbSet<Dashboard> Dashboards => Set<Dashboard>();
        public DbSet<Datachanges> Datachanges => Set<Datachanges>();
        public DbSet<Discussion> Discussions => Set<Discussion>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<Groupmembership> Groupmemberships => Set<Groupmembership>();
        public DbSet<Integration> Integrations => Set<Integration>();
        public DbSet<Intellectualproperty> IntellectualPropertys => Set<Intellectualproperty>();
        public DbSet<Invitation> Invitations => Set<Invitation>();
        public DbSet<Mediafile> Mediafiles => Set<Mediafile>();
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<Organizationmembership> Organizationmemberships =>
            Set<Organizationmembership>();
        public DbSet<Orgdata> Orgdatas => Set<Orgdata>();
        public DbSet<Orgworkflowstep> Orgworkflowsteps => Set<Orgworkflowstep>();
        public DbSet<ParatextToken> Paratexttokens => Set<ParatextToken>();
        public DbSet<Passage> Passages => Set<Passage>();
        public DbSet<Passagestatechange> Passagestatechanges => Set<Passagestatechange>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<Plantype> Plantypes => Set<Plantype>();
        public DbSet<Projdata> Projdatas => Set<Projdata>();
        public DbSet<Projectintegration> Projectintegrations => Set<Projectintegration>();
        public DbSet<Project> Projects => Set<Project>();
        public DbSet<Projecttype> Projecttypes => Set<Projecttype>();
        public DbSet<Resource> Resources => Set<Resource>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Section> Sections => Set<Section>();
        public DbSet<Sectionpassage> Sectionpassages => Set<Sectionpassage>();
        public DbSet<Sectionresource> Sectionresources => Set<Sectionresource>();
        public DbSet<Sectionresourceuser> Sectionresourceusers => Set<Sectionresourceuser>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Userversion> UserVersions => Set<Userversion>();
        public DbSet<Statehistory> Statehistorys => Set<Statehistory>();
        public DbSet<Workflowstep> Workflowsteps => Set<Workflowstep>();
        #endregion

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ICurrentUserContext currentUserContext,
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory
        ) : base(options)
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
            _ = builder.HasPostgresExtension("uuid-ossp");
            //make all query items lowercase to send to postgres...
            LowerCaseDB(builder);

            DefineManyToMany(builder);
            DefineLastModifiedByUser(builder);
        }

        /* On Plan Patch:
         * Server error: An error was generated for warning 'Microsoft.EntityFrameworkCore.Query.NavigationBaseIncludeIgnored':
         * The navigation 'Section.Plan' was ignored from 'Include' in the query since the fix-up will automatically populate it.
         * If any further navigations are specified in 'Include' afterwards then they will be ignored.
         * Walking back include tree is not allowed.
         * This exception can be suppressed or logged by passing event ID 'CoreEventId.NavigationBaseIncludeIgnored' to the 'ConfigureWarnings' method in 'DbContext.OnConfiguring' or 'AddDbContext'.
         **/
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            _ = optionsBuilder.ConfigureWarnings(
                optionsBuilder => optionsBuilder.Ignore(CoreEventId.NavigationBaseIncludeIgnored)
            );
            base.OnConfiguring(optionsBuilder);
        }

        private static void DefineLastModifiedByUser(ModelBuilder builder)
        {
            _ = builder
                .Entity<Activitystate>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Artifactcategory>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Artifacttype>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Comment>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Currentversion>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Datachanges>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Discussion>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Group>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Groupmembership>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Integration>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Intellectualproperty>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Invitation>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Mediafile>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Mediafile>()
                .HasOne(o => o.Passage)
                .WithMany()
                .HasForeignKey(o => o.PassageId);
            _ = builder
                .Entity<Mediafile>()
                .HasOne(o => o.ResourcePassage)
                .WithMany()
                .HasForeignKey(o => o.ResourcePassageId);
            _ = builder
                .Entity<Organization>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Orgdata>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Organizationmembership>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Orgworkflowstep>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Passage>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Passagestatechange>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Plan>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Plantype>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Projdata>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Project>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Projectintegration>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Projecttype>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Resource>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Role>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Section>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Sectionpassage>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Sectionresource>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Sectionresourceuser>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<User>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Userversion>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Statehistory>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Workflowstep>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
        }

        private static void DefineManyToMany(ModelBuilder modelBuilder)
        {
            _ = modelBuilder
                .Entity<Groupmembership>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);


            EntityTypeBuilder<User> userEntity = modelBuilder.Entity<User>();
            _ = userEntity
                .HasMany(u => u.OrganizationMemberships)
                .WithOne(om => om.User)
                .HasForeignKey(om => om.UserId);
            _ = userEntity
                .HasMany(u => u.GroupMemberships)
                .WithOne(gm => gm.User)
                .HasForeignKey(gm => gm.UserId);
            _ = userEntity
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);

            EntityTypeBuilder<Organization> orgEntity = modelBuilder.Entity<Organization>();

            _ = orgEntity.Property(o => o.PublicByDefault).HasDefaultValue(false);
            _ = orgEntity.HasOne(o => o.Owner).WithMany().HasForeignKey(o => o.OwnerId);

            _ = modelBuilder.Entity<Project>().Property(p => p.IsPublic).HasDefaultValue(false);
            _ = modelBuilder.Entity<Project>().HasMany(p => p.Plans).WithOne(pl => pl.Project).HasForeignKey(pl => pl.ProjectId);

            _ = modelBuilder
                .Entity<Orgworkflowstep>()
                .HasOne(s => s.Parent)
                .WithMany()
                .HasForeignKey(s => s.ParentId);
        }

        public async Task<int> SaveChangesNoTimestampAsync(
            CancellationToken cancellationToken = default
        )
        {
            IEnumerable<EntityEntry> entries = ChangeTracker
                .Entries()
                .Where(e =>
                        e.Entity is ILastModified
                        && (e.State == EntityState.Added || e.State == EntityState.Modified)
                );
            foreach (EntityEntry entry in entries)
            {
                entry.CurrentValues ["LastModifiedOrigin"] = "electron";
                if (entry.Entity is ITrackDate trackDate)
                {
                    trackDate.DateCreated = trackDate.DateCreated?.SetKindUtc();
                    trackDate.DateUpdated = trackDate.DateUpdated?.SetKindUtc();
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateSoftDeleteStatuses()
        {
            List<EntityEntry> entries = ChangeTracker
                .Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Deleted)
                .ToList();
            for (int ix = entries.Count - 1; ix >= 0; ix--)
            {
                EntityEntry entry = entries[ix];
                if (entry.Entity is IArchive)
                {
                    switch (entry.State)
                    {
                        case EntityState.Added:
                            entry.CurrentValues ["Archived"] = false;
                            break;
                        case EntityState.Deleted:
                            if ((bool)(entry.CurrentValues ["Archived"] ?? false) == true)
                                entry.State = EntityState.Detached;
                            else
                            {
                                entry.State = EntityState.Modified;
                                entry.CurrentValues ["Archived"] = true;
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
                User? userFromResult = Users.FirstOrDefault(u => (u.ExternalId ?? "").Equals(auth0Id) && !u.Archived);
                _currentUser = userFromResult == null ? -1 : userFromResult.Id;
            }
            return _currentUser;
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            UpdateSoftDeleteStatuses();
            AddTimestamps(this, HttpContext, CurrentUserId());
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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

        //Include all HasOne attributes
        public IQueryable<Artifactcategory> ArtifactcategoriesData =>
            Artifactcategorys.Include(c => c.Organization);
        public IQueryable<Artifacttype> ArtifacttypesData =>
            Artifacttypes.Include(c => c.Organization);
        public IQueryable<Comment> CommentsData => Comments
            .Include(c => c.Mediafile)
            .Include(c => c.Discussion)
            .Include(c => c.LastModifiedByUser);
        public IQueryable<Discussion> DiscussionsData =>
            Discussions
                .Include(d => d.ArtifactCategory)
                .Include(d => d.Mediafile)
                .Include(d => d.OrgWorkflowStep)
                .Include(d => d.Group)
                .Include(d => d.User);
        public IQueryable<Intellectualproperty> IntellectualPropertyData => IntellectualPropertys.Include(x => x.Organization).Include(x => x.ReleaseMediafile);
        public IQueryable<Groupmembership> GroupmembershipsData =>
            Groupmemberships.Include(x => x.Group).Include(x => x.User).Include(x => x.Role);
        public IQueryable<Group> GroupsData => Groups.Include(x => x.Owner);
        public IQueryable<Invitation> InvitationsData =>
            Invitations.Include(x => x.Organization).Include(x => x.Role);
        public IQueryable<Mediafile> MediafilesData =>
            Mediafiles
                .Include(x => x.Passage)
                .Include(x => x.Plan)
                .Include(x => x.ArtifactCategory)
                .Include(x => x.ArtifactType)
                .Include(x => x.ResourcePassage)
                .Include(x => x.SourceMedia);
        public IQueryable<Organizationmembership> OrganizationmembershipsData =>
            Organizationmemberships
                .Include(x => x.Organization)
                .Include(x => x.Role)
                .Include(x => x.User);
        public IQueryable<Organization> OrganizationsData => Organizations.Include(x => x.Owner);
        public IQueryable<Orgworkflowstep> OrgworkflowstepsData =>
            Orgworkflowsteps.Include(x => x.Organization).Include(x => x.Parent);
        public IQueryable<Passage> PassagesData =>
            Passages.Include(x => x.Section).Include(x => x.OrgWorkflowStep);
        public IQueryable<Passagestatechange> PassagestatechangesData => Passagestatechanges
            .Include(x => x.Passage)
            .Include(x => x.LastModifiedByUser);
        public IQueryable<Plan> PlansData =>
            Plans.Include(x => x.Owner).Include(x => x.Plantype).Include(x => x.Project);
        public IQueryable<Projectintegration> ProjectintegrationsData =>
            Projectintegrations.Include(x => x.Project).Include(x => x.Integration);
        public IQueryable<Project> ProjectsData =>
            Projects
                .Include(x => x.Group)
                .Include(x => x.Organization)
                .Include(x => x.Projecttype)
                .Include(x => x.Owner);
        public IQueryable<Sectionresource> SectionresourcesData =>
            Sectionresources
                .Include(x => x.Mediafile)
                .Include(x => x.OrgWorkflowStep)
                .Include(x => x.Section)
                .Include(x => x.Project)
                .Include(x => x.Passage);
        public IQueryable<Sectionresourceuser> SectionresourceusersData =>
            Sectionresourceusers.Include(x => x.SectionResource).Include(x => x.User);
        public IQueryable<Section> SectionsData =>
            Sections.Include(x => x.Plan).Include(x => x.Editor).Include(x => x.Transcriber);
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
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor
        ) : base(
                targetedFields,
                contextResolver,
                resourceGraph,
                resourceFactory,
                constraintProviders,
                loggerFactory,
                resourceDefinitionAccessor
            )
        { }
    }
}
