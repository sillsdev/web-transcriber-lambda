using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SIL.Transcriber.Models
{
    public partial class transcriberContext : DbContext
    {
        public transcriberContext()
        {
        }

        public transcriberContext(DbContextOptions<transcriberContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Book> Books { get; set; }
        public virtual DbSet<Booktype> Booktypes { get; set; }
        public virtual DbSet<XEmails> Emails { get; set; }
        public virtual DbSet<Groupmemberships> Groupmemberships { get; set; }
        public virtual DbSet<Groups> Groups { get; set; }
        public virtual DbSet<Integration> Integrations { get; set; }
        public virtual DbSet<Notifications> Notifications { get; set; }
        public virtual DbSet<Organizationinviterequests> Organizationinviterequests { get; set; }
        public virtual DbSet<Organizationinvites> Organizationinvites { get; set; }
        public virtual DbSet<Organizationmembership> Organizationmemberships { get; set; }
        public virtual DbSet<XOrganizationproductdefinitions> Organizationproductdefinitions { get; set; }
        public virtual DbSet<Organization> Organizations { get; set; }
        public virtual DbSet<Organizationstores> Organizationstores { get; set; }
        public virtual DbSet<Productartifacts> Productartifacts { get; set; }
        public virtual DbSet<XProductbuilds> Productbuilds { get; set; }
        public virtual DbSet<Productdefinitions> Productdefinitions { get; set; }
        public virtual DbSet<Products> Products { get; set; }
        public virtual DbSet<Producttransitions> Producttransitions { get; set; }
        public virtual DbSet<Projectintegration> Projectintegrations { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<Projectuser> Projectusers { get; set; }
        public virtual DbSet<Reviewer> Reviewers { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<Set> Sets { get; set; }
        public virtual DbSet<Storelanguage> Storelanguages { get; set; }
        public virtual DbSet<Stores> Stores { get; set; }
        public virtual DbSet<Storetypes> Storetypes { get; set; }
        public virtual DbSet<XSystemstatuses> Systemstatuses { get; set; }
        public virtual DbSet<Taskmedia> Taskmedia { get; set; }
        public virtual DbSet<Task> Tasks { get; set; }
        public virtual DbSet<Taskset> Tasksets { get; set; }
        public virtual DbSet<Taskstate> Taskstates { get; set; }
        public virtual DbSet<Userrole> Userroles { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Usertask> Usertasks { get; set; }
        public virtual DbSet<Workflowdefinitions> Workflowdefinitions { get; set; }
        public virtual DbSet<Workflowglobalparameter> Workflowglobalparameter { get; set; }
        public virtual DbSet<Workflowinbox> Workflowinbox { get; set; }
        public virtual DbSet<XWorkflowprocessinstance> Workflowprocessinstance { get; set; }
        public virtual DbSet<Workflowprocessinstancepersistence> Workflowprocessinstancepersistence { get; set; }
        public virtual DbSet<Workflowprocessinstancestatus> Workflowprocessinstancestatus { get; set; }
        public virtual DbSet<Workflowprocessscheme> Workflowprocessscheme { get; set; }
        public virtual DbSet<Workflowprocesstimer> Workflowprocesstimer { get; set; }
        public virtual DbSet<Workflowprocesstransitionhistory> Workflowprocesstransitionhistory { get; set; }
        public virtual DbSet<XWorkflowscheme> Workflowscheme { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=transcriber;User Id=postgres;Password=SILpgSU");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>(entity =>
            {
                entity.ToTable("books");

                entity.HasIndex(e => e.Booktypeid)
                    .HasName("fki_fk_books_booktypeid");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('book_id_seq'::regclass)");

                entity.Property(e => e.Booktypeid).HasColumnName("booktypeid");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasColumnType("character varying");

                entity.HasOne(d => d.Booktype)
                    .WithMany(p => p.Books)
                    .HasForeignKey(d => d.Booktypeid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_books_booktypeid");
            });

            modelBuilder.Entity<Booktype>(entity =>
            {
                entity.ToTable("booktypes");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<XEmails>(entity =>
            {
                entity.ToTable("emails");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Bcc).HasColumnName("bcc");

                entity.Property(e => e.Cc).HasColumnName("cc");

                entity.Property(e => e.Contentmodeljson).HasColumnName("contentmodeljson");

                entity.Property(e => e.Contenttemplate).HasColumnName("contenttemplate");

                entity.Property(e => e.Created).HasColumnName("created");

                entity.Property(e => e.Subject).HasColumnName("subject");
            });

            modelBuilder.Entity<Groupmemberships>(entity =>
            {
                entity.ToTable("groupmemberships");

                entity.HasIndex(e => e.Groupid)
                    .HasName("ix_groupmemberships_groupid");

                entity.HasIndex(e => e.Userid)
                    .HasName("ix_groupmemberships_userid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Groupid).HasColumnName("groupid");

                entity.Property(e => e.Userid).HasColumnName("userid");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.Groupmemberships)
                    .HasForeignKey(d => d.Groupid)
                    .HasConstraintName("fk_groupmemberships_groups_groupid");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Groupmemberships)
                    .HasForeignKey(d => d.Userid)
                    .HasConstraintName("fk_groupmemberships_users_userid");
            });

            modelBuilder.Entity<Groups>(entity =>
            {
                entity.ToTable("groups");

                entity.HasIndex(e => e.Ownerid)
                    .HasName("ix_groups_ownerid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Abbreviation).HasColumnName("abbreviation");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Ownerid).HasColumnName("ownerid");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.Groups)
                    .HasForeignKey(d => d.Ownerid)
                    .HasConstraintName("fk_groups_organizations_ownerid");
            });

            modelBuilder.Entity<Integration>(entity =>
            {
                entity.ToTable("integrations");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasColumnType("character varying");

                entity.Property(e => e.Url)
                    .HasColumnName("url")
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<Notifications>(entity =>
            {
                entity.ToTable("notifications");

                entity.HasIndex(e => e.Userid)
                    .HasName("ix_notifications_userid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateemailsent).HasColumnName("dateemailsent");

                entity.Property(e => e.Dateread).HasColumnName("dateread");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Message).HasColumnName("message");

                entity.Property(e => e.Messageid).HasColumnName("messageid");

                entity.Property(e => e.Messagesubstitutionsjson).HasColumnName("messagesubstitutionsjson");

                entity.Property(e => e.Sendemail)
                    .IsRequired()
                    .HasColumnName("sendemail")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Userid).HasColumnName("userid");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Notifications)
                    .HasForeignKey(d => d.Userid)
                    .HasConstraintName("fk_notifications_users_userid");
            });

            modelBuilder.Entity<Organizationinviterequests>(entity =>
            {
                entity.ToTable("organizationinviterequests");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Orgadminemail).HasColumnName("orgadminemail");

                entity.Property(e => e.Websiteurl).HasColumnName("websiteurl");
            });

            modelBuilder.Entity<Organizationinvites>(entity =>
            {
                entity.ToTable("organizationinvites");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Owneremail).HasColumnName("owneremail");

                entity.Property(e => e.Token).HasColumnName("token");
            });

            modelBuilder.Entity<Organizationmembership>(entity =>
            {
                entity.ToTable("organizationmemberships");

                entity.HasIndex(e => e.Organizationid)
                    .HasName("ix_organizationmemberships_organizationid");

                entity.HasIndex(e => e.Userid)
                    .HasName("ix_organizationmemberships_userid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Organizationid).HasColumnName("organizationid");

                entity.Property(e => e.Userid).HasColumnName("userid");

                entity.HasOne(d => d.Organization)
                    .WithMany(p => p.Organizationmemberships)
                    .HasForeignKey(d => d.Organizationid)
                    .HasConstraintName("fk_organizationmemberships_organizations_organizationid");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Organizationmemberships)
                    .HasForeignKey(d => d.Userid)
                    .HasConstraintName("fk_organizationmemberships_users_userid");
            });

            modelBuilder.Entity<XOrganizationproductdefinitions>(entity =>
            {
                entity.ToTable("organizationproductdefinitions");

                entity.HasIndex(e => e.Organizationid)
                    .HasName("ix_organizationproductdefinitions_organizationid");

                entity.HasIndex(e => e.Productdefinitionid)
                    .HasName("ix_organizationproductdefinitions_productdefinitionid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Organizationid).HasColumnName("organizationid");

                entity.Property(e => e.Productdefinitionid).HasColumnName("productdefinitionid");

                entity.HasOne(d => d.Organization)
                    .WithMany(p => p.Organizationproductdefinitions)
                    .HasForeignKey(d => d.Organizationid)
                    .HasConstraintName("fk_organizationproductdefinitions_organizations_organizationid");

                entity.HasOne(d => d.Productdefinition)
                    .WithMany(p => p.Organizationproductdefinitions)
                    .HasForeignKey(d => d.Productdefinitionid)
                    .HasConstraintName("fk_organizationproductdefinitions_productdefinitions_productde");
            });

            modelBuilder.Entity<Organization>(entity =>
            {
                entity.ToTable("organizations");

                entity.HasIndex(e => e.Ownerid)
                    .HasName("ix_organizations_ownerid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Buildengineapiaccesstoken).HasColumnName("buildengineapiaccesstoken");

                entity.Property(e => e.Buildengineurl).HasColumnName("buildengineurl");

                entity.Property(e => e.Logourl).HasColumnName("logourl");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Ownerid).HasColumnName("ownerid");

                entity.Property(e => e.Publicbydefault)
                    .HasColumnName("publicbydefault")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Usedefaultbuildengine)
                    .HasColumnName("usedefaultbuildengine")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Websiteurl).HasColumnName("websiteurl");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.Organizations)
                    .HasForeignKey(d => d.Ownerid)
                    .HasConstraintName("fk_organizations_users_ownerid");
            });

            modelBuilder.Entity<Organizationstores>(entity =>
            {
                entity.ToTable("organizationstores");

                entity.HasIndex(e => e.Organizationid)
                    .HasName("ix_organizationstores_organizationid");

                entity.HasIndex(e => e.Storeid)
                    .HasName("ix_organizationstores_storeid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Organizationid).HasColumnName("organizationid");

                entity.Property(e => e.Storeid).HasColumnName("storeid");

                entity.HasOne(d => d.Organization)
                    .WithMany(p => p.Organizationstores)
                    .HasForeignKey(d => d.Organizationid)
                    .HasConstraintName("fk_organizationstores_organizations_organizationid");

                entity.HasOne(d => d.Store)
                    .WithMany(p => p.Organizationstores)
                    .HasForeignKey(d => d.Storeid)
                    .HasConstraintName("fk_organizationstores_stores_storeid");
            });


            modelBuilder.Entity<Productartifacts>(entity =>
            {
                entity.ToTable("productartifacts");

                entity.HasIndex(e => e.Productbuildid)
                    .HasName("ix_productartifacts_productbuildid");

                entity.HasIndex(e => e.Productid)
                    .HasName("ix_productartifacts_productid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Artifacttype).HasColumnName("artifacttype");

                entity.Property(e => e.Contenttype).HasColumnName("contenttype");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Filesize).HasColumnName("filesize");

                entity.Property(e => e.Productbuildid).HasColumnName("productbuildid");

                entity.Property(e => e.Productid).HasColumnName("productid");

                entity.Property(e => e.Url).HasColumnName("url");

                entity.HasOne(d => d.Productbuild)
                    .WithMany(p => p.Productartifacts)
                    .HasForeignKey(d => d.Productbuildid)
                    .HasConstraintName("fk_productartifacts_productbuilds_productbuildid");

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.Productartifacts)
                    .HasForeignKey(d => d.Productid)
                    .HasConstraintName("fk_productartifacts_products_productid");
            });

            modelBuilder.Entity<XProductbuilds>(entity =>
            {
                entity.ToTable("productbuilds");

                entity.HasIndex(e => e.Productid)
                    .HasName("ix_productbuilds_productid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Buildid).HasColumnName("buildid");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Productid).HasColumnName("productid");

                entity.Property(e => e.Version).HasColumnName("version");

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.Productbuilds)
                    .HasForeignKey(d => d.Productid)
                    .HasConstraintName("fk_productbuilds_products_productid");
            });

            modelBuilder.Entity<Products>(entity =>
            {
                entity.ToTable("products");

                entity.HasIndex(e => e.Productdefinitionid)
                    .HasName("ix_products_productdefinitionid");

                entity.HasIndex(e => e.Projectid)
                    .HasName("ix_products_projectid");

                entity.HasIndex(e => e.Storeid)
                    .HasName("ix_products_storeid");

                entity.HasIndex(e => e.Storelanguageid)
                    .HasName("ix_products_storelanguageid");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Datebuilt).HasColumnName("datebuilt");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Datepublished).HasColumnName("datepublished");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Productdefinitionid).HasColumnName("productdefinitionid");

                entity.Property(e => e.Projectid).HasColumnName("projectid");

                entity.Property(e => e.Publishlink).HasColumnName("publishlink");

                entity.Property(e => e.Storeid).HasColumnName("storeid");

                entity.Property(e => e.Storelanguageid).HasColumnName("storelanguageid");

                entity.Property(e => e.Workflowbuildid).HasColumnName("workflowbuildid");

                entity.Property(e => e.Workflowcomment).HasColumnName("workflowcomment");

                entity.Property(e => e.Workflowjobid).HasColumnName("workflowjobid");

                entity.Property(e => e.Workflowpublishid).HasColumnName("workflowpublishid");

                entity.HasOne(d => d.Productdefinition)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.Productdefinitionid)
                    .HasConstraintName("fk_products_productdefinitions_productdefinitionid");

                entity.HasOne(d => d.Project)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.Projectid)
                    .HasConstraintName("fk_products_projects_projectid");

                entity.HasOne(d => d.Store)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.Storeid)
                    .HasConstraintName("fk_products_stores_storeid");

                entity.HasOne(d => d.Storelanguage)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.Storelanguageid)
                    .HasConstraintName("fk_products_storelanguages_storelanguageid");
            });

            modelBuilder.Entity<Producttransitions>(entity =>
            {
                entity.ToTable("producttransitions");

                entity.HasIndex(e => e.Productid)
                    .HasName("ix_producttransitions_productid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Allowedusernames).HasColumnName("allowedusernames");

                entity.Property(e => e.Command).HasColumnName("command");

                entity.Property(e => e.Datetransition).HasColumnName("datetransition");

                entity.Property(e => e.Destinationstate).HasColumnName("destinationstate");

                entity.Property(e => e.Initialstate).HasColumnName("initialstate");

                entity.Property(e => e.Productid).HasColumnName("productid");

                entity.Property(e => e.Workflowuserid).HasColumnName("workflowuserid");

                entity.HasOne(d => d.Product)
                    .WithMany(p => p.Producttransitions)
                    .HasForeignKey(d => d.Productid)
                    .HasConstraintName("fk_producttransitions_products_productid");
            });

            modelBuilder.Entity<Projectintegration>(entity =>
            {
                entity.ToTable("projectintegrations");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Integrationid).HasColumnName("integrationid");

                entity.Property(e => e.Projectid).HasColumnName("projectid");

                entity.HasOne(d => d.Integration)
                    .WithMany(p => p.Projectintegrations)
                    .HasForeignKey(d => d.Integrationid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_projectintegrations_integrationid");

                entity.HasOne(d => d.Project)
                    .WithMany(p => p.Projectintegrations)
                    .HasForeignKey(d => d.Projectid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_projectintegrations_projectid");
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.ToTable("projects");

                entity.HasIndex(e => e.Groupid)
                    .HasName("ix_projects_groupid");

                entity.HasIndex(e => e.Organizationid)
                    .HasName("ix_projects_organizationid");

                entity.HasIndex(e => e.Ownerid)
                    .HasName("ix_projects_ownerid");

                entity.HasIndex(e => e.Typeid)
                    .HasName("ix_projects_typeid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Allowdownloads)
                    .HasColumnName("allowdownloads")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Automaticbuilds)
                    .HasColumnName("automaticbuilds")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Datearchived).HasColumnName("datearchived");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.Groupid).HasColumnName("groupid");

                entity.Property(e => e.Ispublic)
                    .HasColumnName("ispublic")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Language).HasColumnName("language");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Organizationid).HasColumnName("organizationid");

                entity.Property(e => e.Ownerid).HasColumnName("ownerid");

                entity.Property(e => e.Typeid).HasColumnName("typeid");

                entity.Property(e => e.Workflowprojectid).HasColumnName("workflowprojectid");

                entity.Property(e => e.Workflowprojecturl).HasColumnName("workflowprojecturl");

                entity.HasOne(d => d.Group)
                    .WithMany(p => p.Projects)
                    .HasForeignKey(d => d.Groupid)
                    .HasConstraintName("fk_projects_groups_groupid");

                entity.HasOne(d => d.Organization)
                    .WithMany(p => p.Projects)
                    .HasForeignKey(d => d.Organizationid)
                    .HasConstraintName("fk_projects_organizations_organizationid");

                entity.HasOne(d => d.Owner)
                    .WithMany(p => p.Projects)
                    .HasForeignKey(d => d.Ownerid)
                    .HasConstraintName("fk_projects_users_ownerid");

                entity.HasOne(d => d.Type)
                    .WithMany(p => p.Projects)
                    .HasForeignKey(d => d.Typeid)
                    .HasConstraintName("fk_projects_applicationtypes_typeid");
            });

            modelBuilder.Entity<Projectuser>(entity =>
            {
                entity.ToTable("projectusers");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Projectid).HasColumnName("projectid");

                entity.Property(e => e.Userid).HasColumnName("userid");

                entity.HasOne(d => d.Project)
                    .WithMany(p => p.Projectusers)
                    .HasForeignKey(d => d.Projectid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_projectusers_projectid");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Projectusers)
                    .HasForeignKey(d => d.Userid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_projectusers_userid");
            });

            modelBuilder.Entity<Reviewer>(entity =>
            {
                entity.ToTable("reviewers");

                entity.HasIndex(e => e.Projectid)
                    .HasName("ix_reviewers_projectid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Email).HasColumnName("email");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Projectid).HasColumnName("projectid");

                entity.HasOne(d => d.Project)
                    .WithMany(p => p.Reviewers)
                    .HasForeignKey(d => d.Projectid)
                    .HasConstraintName("fk_reviewers_projects_projectid");
            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("roles");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Rolename)
                    .IsRequired()
                    .HasColumnName("rolename")
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<Set>(entity =>
            {
                entity.ToTable("sets");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Bookid).HasColumnName("bookid");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasColumnType("character varying");

                entity.Property(e => e.Projectid).HasColumnName("projectid");

                entity.HasOne(d => d.Book)
                    .WithMany(p => p.Sets)
                    .HasForeignKey(d => d.Bookid)
                    .HasConstraintName("fk_sets_bookid");

                entity.HasOne(d => d.Project)
                    .WithMany(p => p.Sets)
                    .HasForeignKey(d => d.Projectid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_sets_projectid");
            });

            modelBuilder.Entity<Storelanguage>(entity =>
            {
                entity.ToTable("storelanguages");

                entity.HasIndex(e => e.Storetypeid)
                    .HasName("ix_storelanguages_storetypeid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Storetypeid).HasColumnName("storetypeid");

                entity.HasOne(d => d.Storetype)
                    .WithMany(p => p.Storelanguage)
                    .HasForeignKey(d => d.Storetypeid)
                    .HasConstraintName("fk_storelanguages_storetypes_storetypeid");
            });

            modelBuilder.Entity<Stores>(entity =>
            {
                entity.ToTable("stores");

                entity.HasIndex(e => e.Storetypeid)
                    .HasName("ix_stores_storetypeid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Storetypeid).HasColumnName("storetypeid");

                entity.HasOne(d => d.Storetype)
                    .WithMany(p => p.Stores)
                    .HasForeignKey(d => d.Storetypeid)
                    .HasConstraintName("fk_stores_storetypes_storetypeid");
            });

            modelBuilder.Entity<Storetypes>(entity =>
            {
                entity.ToTable("storetypes");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.Name).HasColumnName("name");
            });

            modelBuilder.Entity<XSystemstatuses>(entity =>
            {
                entity.ToTable("systemstatuses");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Buildengineapiaccesstoken).HasColumnName("buildengineapiaccesstoken");

                entity.Property(e => e.Buildengineurl).HasColumnName("buildengineurl");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Systemavailable).HasColumnName("systemavailable");
            });

            modelBuilder.Entity<Taskmedia>(entity =>
            {
                entity.ToTable("taskmedia");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Artifacttype).HasColumnName("artifacttype");

                entity.Property(e => e.Audioquality).HasColumnName("audioquality");

                entity.Property(e => e.Audiourl).HasColumnName("audiourl");

                entity.Property(e => e.Contenttype).HasColumnName("contenttype");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Duration).HasColumnName("duration");

                entity.Property(e => e.Eafurl).HasColumnName("eafurl");

                entity.Property(e => e.Taskid).HasColumnName("taskid");

                entity.Property(e => e.Textquality).HasColumnName("textquality");

                entity.Property(e => e.Transcription).HasColumnName("transcription");

                entity.Property(e => e.Versionnumber).HasColumnName("versionnumber");

                entity.HasOne(d => d.Task)
                    .WithMany(p => p.Taskmedia)
                    .HasForeignKey(d => d.Taskid)
                    .HasConstraintName("fk_taskmedia_taskid");
            });

            modelBuilder.Entity<Tasks>(entity =>
            {
                entity.ToTable("tasks");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('task_id_seq'::regclass)");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Hold)
                    .HasColumnName("hold")
                    .HasColumnType("bit(1)");

                entity.Property(e => e.Passage).HasColumnName("passage");

                entity.Property(e => e.Position).HasColumnName("position");

                entity.Property(e => e.Reference).HasColumnName("reference");

                entity.Property(e => e.Taskstate).HasColumnName("taskstate");

                entity.Property(e => e.Title).HasColumnName("title");
            });

            modelBuilder.Entity<Taskset>(entity =>
            {
                entity.ToTable("tasksets");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Setid).HasColumnName("setid");

                entity.Property(e => e.Taskid).HasColumnName("taskid");

                entity.HasOne(d => d.Set)
                    .WithMany(p => p.Tasksets)
                    .HasForeignKey(d => d.Setid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_tasksets_setid");

                entity.HasOne(d => d.Task)
                    .WithMany(p => p.Tasksets)
                    .HasForeignKey(d => d.Taskid)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_tasksets_taskid");
            });

            modelBuilder.Entity<Taskstate>(entity =>
            {
                entity.ToTable("taskstates");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.State)
                    .HasColumnName("state")
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<Userrole>(entity =>
            {
                entity.ToTable("userroles");

                entity.HasIndex(e => e.Organizationid)
                    .HasName("ix_userroles_organizationid");

                entity.HasIndex(e => e.Roleid)
                    .HasName("ix_userroles_roleid");

                entity.HasIndex(e => e.Userid)
                    .HasName("ix_userroles_userid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Organizationid).HasColumnName("organizationid");

                entity.Property(e => e.Roleid).HasColumnName("roleid");

                entity.Property(e => e.Userid).HasColumnName("userid");

                entity.HasOne(d => d.Organization)
                    .WithMany(p => p.Userroles)
                    .HasForeignKey(d => d.Organizationid)
                    .HasConstraintName("fk_userroles_organizations_organizationid");

                entity.HasOne(d => d.Role)
                    .WithMany(p => p.Userroles)
                    .HasForeignKey(d => d.Roleid)
                    .HasConstraintName("fk_userroles_roles_roleid");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Userroles)
                    .HasForeignKey(d => d.Userid)
                    .HasConstraintName("fk_userroles_users_userid");
            });

            modelBuilder.Entity<Users>(entity =>
            {
                entity.ToTable("users");

                entity.HasIndex(e => e.Workflowuserid)
                    .HasName("ix_users_workflowuserid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Avatarurl).HasColumnName("avatarurl");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Email).HasColumnName("email");

                entity.Property(e => e.Emailnotification)
                    .HasColumnName("emailnotification")
                    .HasDefaultValueSql("true");

                entity.Property(e => e.Externalid).HasColumnName("externalid");

                entity.Property(e => e.Familyname).HasColumnName("familyname");

                entity.Property(e => e.Givenname).HasColumnName("givenname");

                entity.Property(e => e.Hotkeys)
                    .HasColumnName("hotkeys")
                    .HasColumnType("json");

                entity.Property(e => e.Identitytoken).HasColumnName("identitytoken");

                entity.Property(e => e.Islocked).HasColumnName("islocked");

                entity.Property(e => e.Locale).HasColumnName("locale");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Phone).HasColumnName("phone");

                entity.Property(e => e.Playbackspeed).HasColumnName("playbackspeed");

                entity.Property(e => e.Profilevisibility)
                    .HasColumnName("profilevisibility")
                    .HasDefaultValueSql("1");

                entity.Property(e => e.Progressbartypeid).HasColumnName("progressbartypeid");

                entity.Property(e => e.Publishingkey).HasColumnName("publishingkey");

                entity.Property(e => e.Timercountup).HasColumnName("timercountup");

                entity.Property(e => e.Timezone).HasColumnName("timezone");

                entity.Property(e => e.Uilanguagebcp47).HasColumnName("uilanguagebcp47");

                entity.Property(e => e.Workflowuserid).HasColumnName("workflowuserid");
            });

            modelBuilder.Entity<Usertask>(entity =>
            {
                entity.ToTable("usertasks");

                entity.HasIndex(e => e.Projectid)
                    .HasName("ix_usertasks_projectid");

                entity.HasIndex(e => e.Userid)
                    .HasName("ix_usertasks_userid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Activityname).HasColumnName("activityname");

                entity.Property(e => e.Comment).HasColumnName("comment");

                entity.Property(e => e.Datecreated).HasColumnName("datecreated");

                entity.Property(e => e.Dateupdated).HasColumnName("dateupdated");

                entity.Property(e => e.Projectid).HasColumnName("projectid");

                entity.Property(e => e.Taskid).HasColumnName("taskid");

                entity.Property(e => e.Taskstate).HasColumnName("taskstate");

                entity.Property(e => e.Userid).HasColumnName("userid");

                entity.HasOne(d => d.Project)
                    .WithMany(p => p.Usertasks)
                    .HasForeignKey(d => d.Projectid)
                    .HasConstraintName("fk_usertasks_projectid");

                entity.HasOne(d => d.Task)
                    .WithMany(p => p.Usertasks)
                    .HasForeignKey(d => d.Taskid)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_usertasks_taskid");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Usertasks)
                    .HasForeignKey(d => d.Userid)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_usertasks_userid");
            });

            modelBuilder.Entity<Workflowdefinitions>(entity =>
            {
                entity.ToTable("workflowdefinitions");

                entity.HasIndex(e => e.Storetypeid)
                    .HasName("ix_workflowdefinitions_storetypeid");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Description).HasColumnName("description");

                entity.Property(e => e.Enabled).HasColumnName("enabled");

                entity.Property(e => e.Name).HasColumnName("name");

                entity.Property(e => e.Storetypeid).HasColumnName("storetypeid");

                entity.Property(e => e.Workflowbusinessflow).HasColumnName("workflowbusinessflow");

                entity.Property(e => e.Workflowscheme).HasColumnName("workflowscheme");

                entity.HasOne(d => d.Storetype)
                    .WithMany(p => p.Workflowdefinitions)
                    .HasForeignKey(d => d.Storetypeid)
                    .HasConstraintName("fk_workflowdefinitions_storetypes_storetypeid");
            });

            modelBuilder.Entity<Workflowglobalparameter>(entity =>
            {
                entity.ToTable("workflowglobalparameter");

                entity.HasIndex(e => e.Name)
                    .HasName("workflowglobalparameter_name_idx");

                entity.HasIndex(e => e.Type)
                    .HasName("workflowglobalparameter_type_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(256);

                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasColumnName("type")
                    .HasMaxLength(512);

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnName("value");
            });

            modelBuilder.Entity<Workflowinbox>(entity =>
            {
                entity.ToTable("workflowinbox");

                entity.HasIndex(e => e.Identityid)
                    .HasName("workflowinbox_identityid_idx");

                entity.HasIndex(e => e.Processid)
                    .HasName("workflowinbox_processid_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Identityid)
                    .IsRequired()
                    .HasColumnName("identityid")
                    .HasMaxLength(256);

                entity.Property(e => e.Processid).HasColumnName("processid");
            });

            modelBuilder.Entity<XWorkflowprocessinstance>(entity =>
            {
                entity.ToTable("workflowprocessinstance");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Activityname)
                    .IsRequired()
                    .HasColumnName("activityname")
                    .HasMaxLength(256);

                entity.Property(e => e.Isdeterminingparameterschanged).HasColumnName("isdeterminingparameterschanged");

                entity.Property(e => e.Parentprocessid).HasColumnName("parentprocessid");

                entity.Property(e => e.Previousactivity)
                    .HasColumnName("previousactivity")
                    .HasMaxLength(256);

                entity.Property(e => e.Previousactivityfordirect)
                    .HasColumnName("previousactivityfordirect")
                    .HasMaxLength(256);

                entity.Property(e => e.Previousactivityforreverse)
                    .HasColumnName("previousactivityforreverse")
                    .HasMaxLength(256);

                entity.Property(e => e.Previousstate)
                    .HasColumnName("previousstate")
                    .HasMaxLength(256);

                entity.Property(e => e.Previousstatefordirect)
                    .HasColumnName("previousstatefordirect")
                    .HasMaxLength(256);

                entity.Property(e => e.Previousstateforreverse)
                    .HasColumnName("previousstateforreverse")
                    .HasMaxLength(256);

                entity.Property(e => e.Rootprocessid).HasColumnName("rootprocessid");

                entity.Property(e => e.Schemeid).HasColumnName("schemeid");

                entity.Property(e => e.Statename)
                    .HasColumnName("statename")
                    .HasMaxLength(256);
            });

            modelBuilder.Entity<Workflowprocessinstancepersistence>(entity =>
            {
                entity.ToTable("workflowprocessinstancepersistence");

                entity.HasIndex(e => e.Processid)
                    .HasName("workflowprocessinstancepersistence_processid_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Parametername)
                    .IsRequired()
                    .HasColumnName("parametername")
                    .HasMaxLength(256);

                entity.Property(e => e.Processid).HasColumnName("processid");

                entity.Property(e => e.Value)
                    .IsRequired()
                    .HasColumnName("value");
            });

            modelBuilder.Entity<Workflowprocessinstancestatus>(entity =>
            {
                entity.ToTable("workflowprocessinstancestatus");

                entity.HasIndex(e => e.Status)
                    .HasName("workflowprocessinstancestatus_status_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Lock).HasColumnName("lock");

                entity.Property(e => e.Status).HasColumnName("status");
            });

            modelBuilder.Entity<Workflowprocessscheme>(entity =>
            {
                entity.ToTable("workflowprocessscheme");

                entity.HasIndex(e => e.Definingparametershash)
                    .HasName("workflowprocessscheme_definingparametershash_idx");

                entity.HasIndex(e => e.Isobsolete)
                    .HasName("workflowprocessscheme_isobsolete_idx");

                entity.HasIndex(e => e.Schemecode)
                    .HasName("workflowprocessscheme_schemecode_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Allowedactivities).HasColumnName("allowedactivities");

                entity.Property(e => e.Definingparameters)
                    .IsRequired()
                    .HasColumnName("definingparameters");

                entity.Property(e => e.Definingparametershash)
                    .IsRequired()
                    .HasColumnName("definingparametershash")
                    .HasMaxLength(24);

                entity.Property(e => e.Isobsolete).HasColumnName("isobsolete");

                entity.Property(e => e.Rootschemecode)
                    .HasColumnName("rootschemecode")
                    .HasMaxLength(256);

                entity.Property(e => e.Rootschemeid).HasColumnName("rootschemeid");

                entity.Property(e => e.Scheme)
                    .IsRequired()
                    .HasColumnName("scheme");

                entity.Property(e => e.Schemecode)
                    .IsRequired()
                    .HasColumnName("schemecode")
                    .HasMaxLength(256);

                entity.Property(e => e.Startingtransition).HasColumnName("startingtransition");
            });

            modelBuilder.Entity<Workflowprocesstimer>(entity =>
            {
                entity.ToTable("workflowprocesstimer");

                entity.HasIndex(e => e.Ignore)
                    .HasName("workflowprocesstimer_ignore_idx");

                entity.HasIndex(e => e.Name)
                    .HasName("workflowprocesstimer_name_idx");

                entity.HasIndex(e => e.Nextexecutiondatetime)
                    .HasName("workflowprocesstimer_nextexecutiondatetime_idx");

                entity.HasIndex(e => e.Processid)
                    .HasName("workflowprocesstimer_processid_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Ignore).HasColumnName("ignore");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasMaxLength(256);

                entity.Property(e => e.Nextexecutiondatetime).HasColumnName("nextexecutiondatetime");

                entity.Property(e => e.Processid).HasColumnName("processid");
            });

            modelBuilder.Entity<Workflowprocesstransitionhistory>(entity =>
            {
                entity.ToTable("workflowprocesstransitionhistory");

                entity.HasIndex(e => e.Actoridentityid)
                    .HasName("workflowprocesstransitionhistory_actoridentityid_idx");

                entity.HasIndex(e => e.Executoridentityid)
                    .HasName("workflowprocesstransitionhistory_executoridentityid_idx");

                entity.HasIndex(e => e.Processid)
                    .HasName("workflowprocesstransitionhistory_processid_idx");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                entity.Property(e => e.Actoridentityid)
                    .HasColumnName("actoridentityid")
                    .HasMaxLength(256);

                entity.Property(e => e.Executoridentityid)
                    .HasColumnName("executoridentityid")
                    .HasMaxLength(256);

                entity.Property(e => e.Fromactivityname)
                    .IsRequired()
                    .HasColumnName("fromactivityname")
                    .HasMaxLength(256);

                entity.Property(e => e.Fromstatename)
                    .HasColumnName("fromstatename")
                    .HasMaxLength(256);

                entity.Property(e => e.Isfinalised).HasColumnName("isfinalised");

                entity.Property(e => e.Processid).HasColumnName("processid");

                entity.Property(e => e.Toactivityname)
                    .IsRequired()
                    .HasColumnName("toactivityname")
                    .HasMaxLength(256);

                entity.Property(e => e.Tostatename)
                    .HasColumnName("tostatename")
                    .HasMaxLength(256);

                entity.Property(e => e.Transitionclassifier)
                    .IsRequired()
                    .HasColumnName("transitionclassifier")
                    .HasMaxLength(256);

                entity.Property(e => e.Transitiontime).HasColumnName("transitiontime");

                entity.Property(e => e.Triggername)
                    .HasColumnName("triggername")
                    .HasMaxLength(256);
            });

            modelBuilder.Entity<XWorkflowscheme>(entity =>
            {
                entity.HasKey(e => e.Code);

                entity.ToTable("workflowscheme");

                entity.Property(e => e.Code)
                    .HasColumnName("code")
                    .HasMaxLength(256)
                    .ValueGeneratedNever();

                entity.Property(e => e.Scheme)
                    .IsRequired()
                    .HasColumnName("scheme");
            });

            modelBuilder.HasSequence("applicationtypes_id_seq");

            modelBuilder.HasSequence("book_id_seq");

            modelBuilder.HasSequence("emails_id_seq");

            modelBuilder.HasSequence("groupmemberships_id_seq");

            modelBuilder.HasSequence("groups_id_seq");

            modelBuilder.HasSequence("integrations_id_seq");

            modelBuilder.HasSequence("notifications_id_seq");

            modelBuilder.HasSequence("organizationinviterequests_id_seq");

            modelBuilder.HasSequence("organizationinvites_id_seq");

            modelBuilder.HasSequence("organizationmemberships_id_seq");

            modelBuilder.HasSequence("organizationproductdefinitions_id_seq");

            modelBuilder.HasSequence("organizations_id_seq");

            modelBuilder.HasSequence("organizationstores_id_seq");

            modelBuilder.HasSequence("organizationusers_id_seq");

            modelBuilder.HasSequence("productartifacts_id_seq");

            modelBuilder.HasSequence("productbuilds_id_seq");

            modelBuilder.HasSequence("productdefinitions_id_seq");

            modelBuilder.HasSequence("producttransitions_id_seq");

            modelBuilder.HasSequence("projects_id_seq");

            modelBuilder.HasSequence("reviewers_id_seq");

            modelBuilder.HasSequence("roles_id_seq");

            modelBuilder.HasSequence("storelanguages_id_seq");

            modelBuilder.HasSequence("stores_id_seq");

            modelBuilder.HasSequence("storetypes_id_seq");

            modelBuilder.HasSequence("systemstatuses_id_seq");

            modelBuilder.HasSequence("task_id_seq");

            modelBuilder.HasSequence("taskmedia_id_seq");

            modelBuilder.HasSequence("tasksets_id_seq");

            modelBuilder.HasSequence("userroles_id_seq");

            modelBuilder.HasSequence("users_id_seq");

            modelBuilder.HasSequence("usertasks_id_seq");

            modelBuilder.HasSequence("version_id_seq");

            modelBuilder.HasSequence("workflowdefinitions_id_seq");
        }
    }
}
