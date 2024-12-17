using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIL.Logging.Models;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Data.DbContextExtentions;

namespace SIL.Transcriber.Data
{
    public class LoggingDbContext(
        DbContextOptions<LoggingDbContext> options,
        IHttpContextAccessor httpContextAccessor
        ) : DbContext(options)
    {
        public HttpContext? HttpContext { get; } = httpContextAccessor.HttpContext;
        public DbContextOptions Options { get; } = options;
        public IHttpContextAccessor HttpContextAccessor { get; } = httpContextAccessor;

        private static void DefineLastModifiedByUser(ModelBuilder builder)
        {
            _ = builder
                .Entity<Paratextsync>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Paratextsyncpassage>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<Paratexttokenhistory>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
            _ = builder
                .Entity<User>()
                .HasOne(o => o.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(o => o.LastModifiedBy);
        }

        private static void DefineManyToMany(ModelBuilder modelBuilder)
        {
            EntityTypeBuilder<User> userEntity = modelBuilder.Entity<User>();
            EntityTypeBuilder<Organization> orgEntity = modelBuilder.Entity<Organization>();

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

            _ = orgEntity.Property(o => o.PublicByDefault).HasDefaultValue(true);
            _ = orgEntity.HasOne(o => o.Owner).WithMany().HasForeignKey(o => o.OwnerId);
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

        public override int SaveChanges()
        {
            IEnumerable<EntityEntry> entries = ChangeTracker
                .Entries()
                .Where(e =>
                        e.Entity is ITrackDate
                        && (e.State == EntityState.Added || e.State == EntityState.Modified)
                );
            int userid =
                (entries.FirstOrDefault()?.Entity is LogBaseModel trackUser) ? trackUser.UserId : 0;
            AddTimestamps(this, HttpContext, userid);
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default
        )
        {
            IEnumerable<EntityEntry> entries = ChangeTracker
                .Entries()
                .Where(e =>
                        e.Entity is ITrackDate
                        && (e.State == EntityState.Added || e.State == EntityState.Modified)
                );
            int userid =
                (entries.FirstOrDefault()?.Entity is LogBaseModel trackUser) ? trackUser.UserId : 0;
            AddTimestamps(this, HttpContext, userid);
            return await base.SaveChangesAsync(cancellationToken);
        }

        public DbSet<Paratextsync> Paratextsyncs => Set<Paratextsync>();
        public DbSet<Paratextsyncpassage> Paratextsyncpassages => Set<Paratextsyncpassage>();
        public DbSet<Paratexttokenhistory> Paratexttokenhistory => Set<Paratexttokenhistory>();
    }

    public class LoggingDbContextResolver(LoggingDbContext context) : IDbContextResolver
    {
        private readonly LoggingDbContext _context = context;

        public DbContext GetContext()
        {
            return _context;
        }

        public DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
        {
            return _context.Set<TEntity>();
        }
    }

    public class LoggingDbContextRepository<TResource>(
        ITargetedFields targetedFields,
        LoggingDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor
        )
        : EntityFrameworkCoreRepository<TResource, int>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor
            ) where TResource : class, IIdentifiable<int>
    {
    }
}
