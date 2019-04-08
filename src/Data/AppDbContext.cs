

using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        { }

        //make all query items lowercase to send to postgres...
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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

        public  DbSet<Book> Books { get; set; }
        public  DbSet<BookType> Booktypes { get; set; }
        public  DbSet<Integration> Integrations { get; set; }
        public  DbSet<Organization> Organizations { get; set; }
        public  DbSet<OrganizationMembership> Organizationmemberships { get; set; }
        public  DbSet<Project> Projects { get; set; }
        public  DbSet<ProjectIntegration> Projectintegrations { get; set; }
        public  DbSet<ProjectType> ProjectTypes { get; set; }
        public  DbSet<ProjectUser> Projectusers { get; set; }
        public  DbSet<Reviewer> Reviewers { get; set; }
        public  DbSet<Role> Roles { get; set; }
        public  DbSet<Set> Sets { get; set; }
        public  DbSet<TaskMedia> Taskmedia { get; set; }
        public  DbSet<Task> Tasks { get; set; }
        public  DbSet<TaskSet> Tasksets { get; set; }
        public  DbSet<TaskState> Taskstates { get; set; }
        public DbSet<UserRole> Userroles { get; set; }
        public  DbSet<User> Users { get; set; }
        public  DbSet<UserTask> Usertasks { get; set; }
    }
}
