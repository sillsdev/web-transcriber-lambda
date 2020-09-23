using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Transcriber.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIL.Transcriber.Data
{
    public class LoggingDbContext : BaseDbContext
    {
        public LoggingDbContext(DbContextOptions<LoggingDbContext> options,  IHttpContextAccessor httpContextAccessor) :
             base(options, httpContextAccessor)
        {
        }
        public override int SaveChanges()
        {
            System.Collections.Generic.IEnumerable<EntityEntry> entries = ChangeTracker.Entries().Where(e => e.Entity is ITrackDate && (e.State == EntityState.Added || e.State == EntityState.Modified));
            int userid = (entries.FirstOrDefault().Entity is LogBaseModel trackUser) ? trackUser.UserId : 0;          
            AddTimestamps(userid);
            return base.SaveChanges();
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            System.Collections.Generic.IEnumerable<EntityEntry> entries = ChangeTracker.Entries().Where(e => e.Entity is ITrackDate && (e.State == EntityState.Added || e.State == EntityState.Modified));
            int userid = (entries.FirstOrDefault().Entity is LogBaseModel trackUser) ? trackUser.UserId : 0;
            AddTimestamps(userid);
            return await base.SaveChangesAsync(cancellationToken);
        }
        public DbSet<ParatextSync> Paratextsyncs { get; set; }
        public DbSet<ParatextSyncPassage> Paratextsyncpassages { get; set; }
        public DbSet<ParatextTokenHistory> Paratexttokenhistory { get; set; }
    }
    public class LoggingDbContextResolver : IDbContextResolver
    {
        private readonly LoggingDbContext _context;
        public LoggingDbContextResolver(LoggingDbContext context)
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

    public class LoggingDbContextRepository<TResource> : DefaultEntityRepository<TResource>
    where TResource : class, IIdentifiable<int>
    {
        public LoggingDbContextRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            LoggingDbContextResolver contextResolver
           ) : base(loggerFactory, jsonApiContext, contextResolver)
        { }
    }

}
