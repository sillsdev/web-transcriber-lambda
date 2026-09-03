using Identity = Auth0.ManagementApi.Models.Identity;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SIL.Paratext.Models;
using SIL.Transcriber;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serializers;
using SIL.Transcriber.Services;
using SIL.Transcriber.Services.Contracts;
using System.IO.Compression;
using System.Net;
using Xunit;

namespace TranscriberAPI.Tests.Services;

public class OfflineDataServiceTests
{
    private const string SourceOrganizationId = "1001";
    private const string ExtraOrganizationId = "1002";
    private const string SourceProjectId = "1010";
    private const string SourcePlanId = "1020";
    private const string SourceSectionId = "1030";
    private const string SourcePrimaryPassageId = "1040";
    private const string SourceSupportingPassageId = "1041";
    private const string SourceSupportingSharedResourceId = "1050";
    private const string SourceSupportingReferenceId = "1060";
    private const string SourceSupportingCategoryId = "1070";
    private const int BatchSourcePassageBaseId = 7000;
    private const int BatchSharedResourceBaseId = 9000;
    private const int BatchSectionBaseId = 11000;

    [Fact]
    public async Task ProcessImportCopyFileAsync_PreservesSupportingNotesAcrossResumeWithExtraOrganizations()
    {
        string databaseName = $"offline-import-{Guid.NewGuid():N}";
        await using ServiceProvider provider = BuildServiceProvider(databaseName);

        int fullImportTargetOrgId;
        int resumedImportTargetOrgId;
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            SeedLookupData(dbContext);
            SeedCurrentUser(dbContext);
            fullImportTargetOrgId = SeedTargetOrganization(dbContext, "Import Target A");
            resumedImportTargetOrgId = SeedTargetOrganization(dbContext, "Import Target B");
            await dbContext.SaveChangesAsync();
        }

        byte[] archiveBytes;
        int sharedResourcesStartIndex;
        await using (AsyncServiceScope archiveScope = provider.CreateAsyncScope())
        {
            archiveBytes = CreateArchive(archiveScope.ServiceProvider);
            sharedResourcesStartIndex = GetDataEntryIndex(archiveBytes, "I_sharedresources.json");
        }

        const string fullMapKey = "full-import-map";
        await using (AsyncServiceScope fullImportScope = provider.CreateAsyncScope())
        {
            OfflineDataService service = (OfflineDataService)fullImportScope.ServiceProvider.GetRequiredService<IOfflineDataService>();
            using ZipArchive archive = OpenArchive(archiveBytes);
            Fileresponse response = await service.ProcessImportCopyFileAsync(archive, fullImportTargetOrgId, "full.ptf", 0, fullMapKey);
            Assert.True(response.Status == HttpStatusCode.OK, response.Message);
        }

        ImportedSupportingNoteState fullImportState;
        await using (AsyncServiceScope fullAssertScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = fullAssertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Contains(dbContext.Copyprojects, cp => cp.Newprojid == fullMapKey && cp.Sourcetable == Tables.Organizations && cp.Oldid == ExtraOrganizationId && cp.Newid == fullImportTargetOrgId);
            fullImportState = LoadImportedSupportingNoteState(dbContext, fullMapKey);
        }

        const string resumedMapKey = "resumed-import-map";
        await using (AsyncServiceScope resumeSeedScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = resumeSeedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedResumedImportState(dbContext, resumedImportTargetOrgId, resumedMapKey);
            await dbContext.SaveChangesAsync();
        }

        await using (AsyncServiceScope resumedImportScope = provider.CreateAsyncScope())
        {
            OfflineDataService service = (OfflineDataService)resumedImportScope.ServiceProvider.GetRequiredService<IOfflineDataService>();
            using ZipArchive archive = OpenArchive(archiveBytes);
            Fileresponse response = await service.ProcessImportCopyFileAsync(archive, resumedImportTargetOrgId, "resume.ptf", sharedResourcesStartIndex, resumedMapKey);
            Assert.True(response.Status == HttpStatusCode.OK, response.Message);
        }

        await using (AsyncServiceScope resumedAssertScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = resumedAssertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            ImportedSupportingNoteState resumedState = LoadImportedSupportingNoteState(dbContext, resumedMapKey);

            Assert.NotEqual(fullImportState.ProjectId, resumedState.ProjectId);
            Assert.Equal(fullImportState.SupportingResourceTitle, resumedState.SupportingResourceTitle);
            Assert.Equal(fullImportState.SupportingReferenceBook, resumedState.SupportingReferenceBook);
            Assert.Equal(fullImportState.SupportingReferenceChapter, resumedState.SupportingReferenceChapter);
            Assert.Equal(fullImportState.SupportingReferenceVerses, resumedState.SupportingReferenceVerses);
            Assert.Equal(fullImportState.ImportedPassageCount, resumedState.ImportedPassageCount);
            Assert.Equal(fullImportState.SharedResourceCount, resumedState.SharedResourceCount);
            Assert.Equal(fullImportState.SharedResourceReferenceCount, resumedState.SharedResourceReferenceCount);
            Assert.Equal(fullImportState.SupportingCategoryName, resumedState.SupportingCategoryName);
        }
    }

    [Fact]
    public async Task ProcessImportCopyFileAsync_SharedResourcesBatchingOnResume_DoesNotCreateDuplicateMappings()
    {
        string databaseName = $"offline-import-batch-{Guid.NewGuid():N}";
        await using ServiceProvider provider = BuildServiceProvider(databaseName);

        int targetOrgId;
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            SeedLookupData(dbContext);
            SeedCurrentUser(dbContext);
            targetOrgId = SeedTargetOrganization(dbContext, "Import Target Batch");
            await dbContext.SaveChangesAsync();
        }

        const int sharedResourceCount = 130;
        byte[] archiveBytes;
        int sharedResourcesStartIndex;
        await using (AsyncServiceScope archiveScope = provider.CreateAsyncScope())
        {
            archiveBytes = CreateArchiveWithSharedResources(archiveScope.ServiceProvider, sharedResourceCount);
            sharedResourcesStartIndex = GetDataEntryIndex(archiveBytes, "I_sharedresources.json");
        }

        const string mapKey = "shared-resume-map";
        await using (AsyncServiceScope resumeSeedScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = resumeSeedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedResumedImportStateForBatch(dbContext, targetOrgId, mapKey, sharedResourceCount);
            await dbContext.SaveChangesAsync();
        }

        await using (AsyncServiceScope importScope = provider.CreateAsyncScope())
        {
            OfflineDataService service = (OfflineDataService)importScope.ServiceProvider.GetRequiredService<IOfflineDataService>();
            using ZipArchive archive = OpenArchive(archiveBytes);
            Fileresponse response = await service.ProcessImportCopyFileAsync(archive, targetOrgId, "resume-many.ptf", sharedResourcesStartIndex, mapKey);
            Assert.True(response.Status == HttpStatusCode.OK, response.Message);
        }

        await using (AsyncServiceScope assertScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            List<CopyProject> sharedResourceMaps = [.. dbContext.Copyprojects
                .Where(cp => cp.Newprojid == mapKey && cp.Sourcetable == Tables.SharedResources)];

            Assert.Equal(sharedResourceCount, sharedResourceMaps.Count);
            Assert.Equal(sharedResourceCount, sharedResourceMaps.Select(cp => cp.Oldid).Distinct().Count());
            Assert.DoesNotContain(sharedResourceMaps, cp => cp.Newid <= 0);
        }
    }

    [Fact]
    public async Task ProcessImportCopyFileAsync_SectionsBatchingOnResume_DoesNotSkipPendingSections()
    {
        string databaseName = $"offline-import-sections-{Guid.NewGuid():N}";
        await using ServiceProvider provider = BuildServiceProvider(databaseName);

        int targetOrgId;
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            SeedLookupData(dbContext);
            SeedCurrentUser(dbContext);
            targetOrgId = SeedTargetOrganization(dbContext, "Import Target Sections");
            await dbContext.SaveChangesAsync();
        }

        const int sectionCount = 130;
        byte[] archiveBytes;
        int sectionsStartIndex;
        await using (AsyncServiceScope archiveScope = provider.CreateAsyncScope())
        {
            archiveBytes = CreateArchiveWithSections(archiveScope.ServiceProvider, sectionCount);
            sectionsStartIndex = GetDataEntryIndex(archiveBytes, "F_sections.json");
        }

        const string mapKey = "sections-resume-map";
        await using (AsyncServiceScope resumeSeedScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = resumeSeedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            SeedResumedImportStateForSectionsBatch(dbContext, targetOrgId, mapKey);
            await dbContext.SaveChangesAsync();
        }

        await using (AsyncServiceScope importScope = provider.CreateAsyncScope())
        {
            OfflineDataService service = (OfflineDataService)importScope.ServiceProvider.GetRequiredService<IOfflineDataService>();
            using ZipArchive archive = OpenArchive(archiveBytes);
            Fileresponse response = await service.ProcessImportCopyFileAsync(archive, targetOrgId, "resume-sections.ptf", sectionsStartIndex, mapKey);
            Assert.True(response.Status == HttpStatusCode.OK, response.Message);
        }

        await using (AsyncServiceScope assertScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            List<CopyProject> sectionMaps = [.. dbContext.Copyprojects
                .Where(cp => cp.Newprojid == mapKey && cp.Sourcetable == Tables.Sections)];

            Assert.Equal(sectionCount, sectionMaps.Count);
            Assert.Equal(sectionCount, sectionMaps.Select(cp => cp.Oldid).Distinct().Count());
            Assert.DoesNotContain(sectionMaps, cp => cp.Newid <= 0);
        }
    }

    [Fact]
    public async Task ExportProjectPTF_RestrictsForeignOrganizationCategoriesToSupportingSharedResourceCategories()
    {
        string databaseName = $"offline-export-{Guid.NewGuid():N}";
        await using ServiceProvider provider = BuildServiceProvider(databaseName);

        ExportFixture fixture;
        await using (AsyncServiceScope setupScope = provider.CreateAsyncScope())
        {
            AppDbContext dbContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            SeedLookupData(dbContext);
            SeedCurrentUser(dbContext);
            fixture = SeedExportCategoryFixture(dbContext);
            await dbContext.SaveChangesAsync();
        }

        byte[] archiveBytes;
        await using (AsyncServiceScope exportScope = provider.CreateAsyncScope())
        {
            OfflineDataService service = (OfflineDataService)exportScope.ServiceProvider.GetRequiredService<IOfflineDataService>();
            FakeS3Service s3Service = (FakeS3Service)exportScope.ServiceProvider.GetRequiredService<IS3Service>();
            Fileresponse response = service.ExportProjectPTF(fixture.ProjectId, 0);
            Assert.Equal(HttpStatusCode.PartialContent, response.Status);
            archiveBytes = s3Service.GetFileBytes("exports", response.Message);
        }

        HashSet<string> exportedCategoryIds = GetEntryIds(archiveBytes, "C_artifactcategorys.json");
        HashSet<string> exportedMediafileIds = GetEntryIds(archiveBytes, "H_mediafiles.json");

        Assert.Contains(fixture.GlobalCategoryId.ToString(), exportedCategoryIds);
        Assert.Contains(fixture.PrimaryCategoryId.ToString(), exportedCategoryIds);
        Assert.Contains(fixture.ForeignUsedCategoryId.ToString(), exportedCategoryIds);
        Assert.DoesNotContain(fixture.ForeignUnusedCategoryId.ToString(), exportedCategoryIds);

        Assert.Contains(fixture.ForeignUsedTitleMediaId.ToString(), exportedMediafileIds);
        Assert.DoesNotContain(fixture.ForeignUnusedTitleMediaId.ToString(), exportedMediafileIds);
    }

    private static ServiceProvider BuildServiceProvider(string databaseName)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUserContext, FakeCurrentUserContext>();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<AppDbContextResolver>();
        services.AddJsonApi<AppDbContext>(
            options =>
            {
                options.DefaultPageSize = null;
                options.Namespace = "api";
                options.UseRelativeLinks = true;
                options.IncludeTotalResourceCount = false;
                options.SerializerOptions.WriteIndented = false;
                options.ResourceLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                options.RelationshipLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                options.TopLevelLinks = JsonApiDotNetCore.Resources.Annotations.LinkTypes.None;
                options.AllowUnknownQueryStringParameters = true;
                options.AllowUnknownFieldsInRequestBody = true;
                options.MaximumIncludeDepth = 2;
                options.EnableLegacyFilterNotation = true;
            },
            discovery => discovery.AddAssembly(typeof(Project).Assembly));
        services.RegisterRepositories();
        services.RegisterServices();
        services.AddSingleton<IS3Service, FakeS3Service>();
        services.AddSingleton<ISQSService, FakeSqsService>();
        return services.BuildServiceProvider();
    }

    private static void SeedLookupData(AppDbContext dbContext)
    {
        dbContext.Projecttypes.Add(new Projecttype { Id = 1, Name = "Story" });
        dbContext.Plantypes.Add(new Plantype { Id = 1, Name = "Standard" });
        dbContext.Passagetypes.Add(new Passagetype { Id = 1, Abbrev = "SCR", USFM = "GEN", Title = "Scripture" });
        dbContext.Roles.AddRange(
            new Role { Id = 2, Rolename = RoleName.Admin, Orgrole = true },
            new Role { Id = 5, Rolename = RoleName.Member, Orgrole = true },
            new Role { Id = 11, Rolename = RoleName.Admin, Grouprole = true },
            new Role { Id = 12, Rolename = RoleName.Member, Grouprole = true });
    }

    private static void SeedCurrentUser(AppDbContext dbContext)
    {
        dbContext.Users.Add(new User
        {
            Id = 1,
            Name = "Test User",
            Email = "test@example.com",
            ExternalId = FakeCurrentUserContext.TestAuth0Id
        });
    }

    private static int SeedTargetOrganization(AppDbContext dbContext, string name)
    {
        Organization organization = new()
        {
            Name = name,
            OwnerId = 1,
            Slug = name.ToLowerInvariant().Replace(' ', '-')
        };
        dbContext.Organizations.Add(organization);
        dbContext.SaveChanges();
        dbContext.Groups.Add(new Group
        {
            Name = $"All users of {name}",
            Abbreviation = "all-users",
            AllUsers = true,
            OwnerId = organization.Id
        });
        dbContext.SaveChanges();
        return organization.Id;
    }

    private static ExportFixture SeedExportCategoryFixture(AppDbContext dbContext)
    {
        Organization primaryOrganization = new()
        {
            Id = 2001,
            Name = "Export Primary Organization",
            OwnerId = 1,
            Slug = "export-primary"
        };
        Organization foreignOrganization = new()
        {
            Id = 2002,
            Name = "Export Foreign Organization",
            OwnerId = 1,
            Slug = "export-foreign"
        };
        Group primaryGroup = new()
        {
            Id = 2101,
            Name = "All users of Export Primary Organization",
            Abbreviation = "all-users",
            AllUsers = true,
            OwnerId = primaryOrganization.Id
        };
        Project project = new()
        {
            Id = 2201,
            Name = "Category Export Project",
            OrganizationId = primaryOrganization.Id,
            GroupId = primaryGroup.Id,
            OwnerId = 1,
            ProjecttypeId = 1
        };
        Plan plan = new()
        {
            Id = 2301,
            Name = "Category Export Plan",
            ProjectId = project.Id,
            PlantypeId = 1,
            OwnerId = 1,
            Flat = false,
            SectionCount = 1
        };
        Section section = new()
        {
            Id = 2401,
            Name = "Category Export Section",
            PlanId = plan.Id,
            Sequencenum = 1,
            Level = 1,
            Published = true,
            State = "assigned",
            PublishTo = "{}"
        };
        Passage passage = new()
        {
            Id = 2501,
            Title = "Category Export Passage",
            Book = "GEN",
            Reference = "1:1",
            State = "approved",
            Sequencenum = 1,
            SectionId = section.Id,
            PassagetypeId = 1
        };
        Artifactcategory globalCategory = new()
        {
            Id = 2701,
            Categoryname = "Global Category",
            OrganizationId = null
        };
        Artifactcategory primaryCategory = new()
        {
            Id = 2702,
            Categoryname = "Primary Category",
            OrganizationId = primaryOrganization.Id
        };
        Artifactcategory foreignUsedCategory = new()
        {
            Id = 2703,
            Categoryname = "Foreign Used Category",
            OrganizationId = foreignOrganization.Id,
            TitleMediafileId = 2801
        };
        Artifactcategory foreignUnusedCategory = new()
        {
            Id = 2704,
            Categoryname = "Foreign Unused Category",
            OrganizationId = foreignOrganization.Id,
            TitleMediafileId = 2802
        };
        Sharedresource supportingSharedResource = new()
        {
            Id = 2601,
            Title = "Foreign Supporting Note",
            Note = true,
            PassageId = passage.Id,
            ArtifactCategoryId = foreignUsedCategory.Id
        };
        Note supportingNote = new()
        {
            Id = 2602,
            ProjectId = project.Id,
            OrganizationId = primaryOrganization.Id,
            PlanId = plan.Id,
            SectionId = section.Id,
            PassageId = passage.Id,
            ResourceId = supportingSharedResource.Id,
            Title = supportingSharedResource.Title,
            Book = passage.Book,
            Reference = passage.Reference
        };
        VWProject sharedNoteView = new()
        {
            Id = 2603,
            OrganizationId = primaryOrganization.Id,
            OrgName = primaryOrganization.Name ?? string.Empty,
            ProjectId = project.Id,
            ProjectName = project.Name,
            Language = "en",
            SectionId = section.Id,
            SectionTitle = section.Name,
            SectionNum = section.Sequencenum,
            PassageId = passage.Id,
            PassageNum = passage.Sequencenum,
            Book = passage.Book,
            Reference = passage.Reference,
            SharedResourceId = supportingSharedResource.Id
        };
        Mediafile usedCategoryTitleMedia = new()
        {
            Id = 2801,
            PlanId = 9991,
            S3File = "used-category-title.mp3",
            ContentType = "audio/mpeg"
        };
        Mediafile unusedCategoryTitleMedia = new()
        {
            Id = 2802,
            PlanId = 9992,
            S3File = "unused-category-title.mp3",
            ContentType = "audio/mpeg"
        };

        dbContext.Organizations.AddRange(primaryOrganization, foreignOrganization);
        dbContext.Groups.Add(primaryGroup);
        dbContext.Projects.Add(project);
        dbContext.Plans.Add(plan);
        dbContext.Sections.Add(section);
        dbContext.Passages.Add(passage);
        dbContext.Artifactcategorys.AddRange(globalCategory, primaryCategory, foreignUsedCategory, foreignUnusedCategory);
        dbContext.Sharedresources.Add(supportingSharedResource);
        dbContext.Notes.Add(supportingNote);
        dbContext.VWProjects.Add(sharedNoteView);
        dbContext.Mediafiles.AddRange(usedCategoryTitleMedia, unusedCategoryTitleMedia);

        return new ExportFixture(project.Id, globalCategory.Id, primaryCategory.Id, foreignUsedCategory.Id, foreignUnusedCategory.Id, usedCategoryTitleMedia.Id, unusedCategoryTitleMedia.Id);
    }

    private static byte[] CreateArchiveWithSharedResources(IServiceProvider serviceProvider, int sharedResourceCount)
    {
        Organization sourceOrganization = new()
        {
            Id = int.Parse(SourceOrganizationId),
            Name = "Archive Source Organization",
            Slug = "archive-source-organization"
        };
        Project sourceProject = new()
        {
            Id = int.Parse(SourceProjectId),
            Name = "Archive Source Project",
            Organization = sourceOrganization,
            OrganizationId = sourceOrganization.Id,
            ProjecttypeId = 1,
            Projecttype = new Projecttype { Id = 1, Name = "Story" }
        };
        Plan sourcePlan = new()
        {
            Id = int.Parse(SourcePlanId),
            Name = "Archive Source Plan",
            Project = sourceProject,
            ProjectId = sourceProject.Id,
            PlantypeId = 1,
            Plantype = new Plantype { Id = 1, Name = "Standard" }
        };
        Section sourceSection = new()
        {
            Id = int.Parse(SourceSectionId),
            Name = "Archive Section",
            Plan = sourcePlan,
            PlanId = sourcePlan.Id,
            Sequencenum = 1,
            Level = 1,
            Published = true,
            State = "assigned",
            PublishTo = "{}"
        };
        List<Passage> sourcePassages = [];
        List<Sharedresource> sharedResources = [];
        List<Sharedresourcereference> references = [];
        for (int i = 0; i < sharedResourceCount; i++)
        {
            int sourcePassageId = BatchSourcePassageBaseId + i;
            sourcePassages.Add(new Passage
            {
                Id = sourcePassageId,
                Title = $"Batch Imported Passage {i}",
                Book = "GEN",
                Reference = "1:1",
                State = "approved",
                Sequencenum = i + 1,
                Section = sourceSection,
                SectionId = sourceSection.Id,
                PassagetypeId = 1,
                Passagetype = new Passagetype { Id = 1, Abbrev = "SCR", USFM = "GEN", Title = "Scripture" }
            });
            int sharedResourceId = BatchSharedResourceBaseId + i;
            sharedResources.Add(new Sharedresource
            {
                Id = sharedResourceId,
                Title = $"Batch Supporting Note {i}",
                Description = "Batch shared note",
                Note = true,
                PassageId = sourcePassageId
            });
            references.Add(new Sharedresourcereference
            {
                Id = 12000 + i,
                SharedResourceId = sharedResourceId,
                Book = "GEN",
                Chapter = 1,
                Verses = "1"
            });
        }

        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "SILTranscriber", DateTime.UtcNow.ToString("o"));
            AddJsonEntry(archive, "data/B_organizations.json", new[] { sourceOrganization }, serviceProvider);
            AddJsonEntry(archive, "data/D_projects.json", new[] { sourceProject }, serviceProvider);
            AddJsonEntry(archive, "data/E_plans.json", new[] { sourcePlan }, serviceProvider);
            AddJsonEntry(archive, "data/F_sections.json", new[] { sourceSection }, serviceProvider);
            AddJsonEntry(archive, "data/G_passages.json", sourcePassages, serviceProvider);
            AddJsonEntry(archive, "data/I_sharedresources.json", sharedResources, serviceProvider);
            AddJsonEntry(archive, "data/J_sharedresourcereferences.json", references, serviceProvider);
        }
        return stream.ToArray();
    }

    private static byte[] CreateArchiveWithSections(IServiceProvider serviceProvider, int sectionCount)
    {
        Organization sourceOrganization = new()
        {
            Id = int.Parse(SourceOrganizationId),
            Name = "Archive Source Organization",
            Slug = "archive-source-organization"
        };
        Project sourceProject = new()
        {
            Id = int.Parse(SourceProjectId),
            Name = "Archive Source Project",
            Organization = sourceOrganization,
            OrganizationId = sourceOrganization.Id,
            ProjecttypeId = 1,
            Projecttype = new Projecttype { Id = 1, Name = "Story" }
        };
        Plan sourcePlan = new()
        {
            Id = int.Parse(SourcePlanId),
            Name = "Archive Source Plan",
            Project = sourceProject,
            ProjectId = sourceProject.Id,
            PlantypeId = 1,
            Plantype = new Plantype { Id = 1, Name = "Standard" }
        };

        List<Section> sections = [];
        for (int i = 0; i < sectionCount; i++)
        {
            sections.Add(new Section
            {
                Id = BatchSectionBaseId + i,
                Name = $"Batch Section {i}",
                Plan = sourcePlan,
                PlanId = sourcePlan.Id,
                Sequencenum = i + 1,
                Level = 1,
                Published = true,
                State = "assigned",
                PublishTo = "{}"
            });
        }

        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "SILTranscriber", DateTime.UtcNow.ToString("o"));
            AddJsonEntry(archive, "data/B_organizations.json", new[] { sourceOrganization }, serviceProvider);
            AddJsonEntry(archive, "data/D_projects.json", new[] { sourceProject }, serviceProvider);
            AddJsonEntry(archive, "data/E_plans.json", new[] { sourcePlan }, serviceProvider);
            AddJsonEntry(archive, "data/F_sections.json", sections, serviceProvider);
        }
        return stream.ToArray();
    }

    private static byte[] CreateArchive(IServiceProvider serviceProvider)
    {
        Organization sourceOrganization = new()
        {
            Id = int.Parse(SourceOrganizationId),
            Name = "Archive Source Organization",
            Slug = "archive-source-organization"
        };
        Organization extraOrganization = new()
        {
            Id = int.Parse(ExtraOrganizationId),
            Name = "Archive Extra Organization",
            Slug = "archive-extra-organization"
        };
        Project sourceProject = new()
        {
            Id = int.Parse(SourceProjectId),
            Name = "Archive Source Project",
            Organization = sourceOrganization,
            OrganizationId = sourceOrganization.Id,
            ProjecttypeId = 1,
            Projecttype = new Projecttype { Id = 1, Name = "Story" }
        };
        Plan sourcePlan = new()
        {
            Id = int.Parse(SourcePlanId),
            Name = "Archive Source Plan",
            Project = sourceProject,
            ProjectId = sourceProject.Id,
            PlantypeId = 1,
            Plantype = new Plantype { Id = 1, Name = "Standard" }
        };
        Section sourceSection = new()
        {
            Id = int.Parse(SourceSectionId),
            Name = "Archive Section",
            Plan = sourcePlan,
            PlanId = sourcePlan.Id,
            Sequencenum = 1,
            Level = 1,
            Published = true,
            State = "assigned",
            PublishTo = "{}"
        };
        Artifactcategory supportingCategory = new()
        {
            Id = int.Parse(SourceSupportingCategoryId),
            Categoryname = "Supporting Note Category",
            Note = true,
            Resource = true,
            Discussion = false,
            OrganizationId = int.Parse(ExtraOrganizationId)
        };
        Sharedresource supportingSharedResource = new()
        {
            Id = int.Parse(SourceSupportingSharedResourceId),
            Title = "Supporting Note",
            Description = "Supporting note owned by a skipped passage",
            Note = true,
            PassageId = int.Parse(SourceSupportingPassageId),
            ArtifactCategoryId = supportingCategory.Id,
            ArtifactCategory = supportingCategory
        };
        Passage sourcePrimaryPassage = new()
        {
            Id = int.Parse(SourcePrimaryPassageId),
            Title = "Imported Passage",
            Book = "GEN",
            Reference = "1:1",
            State = "approved",
            Sequencenum = 1,
            Section = sourceSection,
            SectionId = sourceSection.Id,
            SharedResource = supportingSharedResource,
            SharedResourceId = supportingSharedResource.Id,
            PassagetypeId = 1,
            Passagetype = new Passagetype { Id = 1, Abbrev = "SCR", USFM = "GEN", Title = "Scripture" }
        };
        Passage sourceSupportingPassage = new()
        {
            Id = int.Parse(SourceSupportingPassageId),
            Title = "Skipped Supporting Passage",
            Book = "GEN",
            Reference = "1:1 note",
            State = "approved",
            Sequencenum = 2,
            PassagetypeId = 1,
            Passagetype = new Passagetype { Id = 1, Abbrev = "SCR", USFM = "GEN", Title = "Scripture" }
        };
        supportingSharedResource.Passage = sourceSupportingPassage;
        Sharedresourcereference supportingReference = new()
        {
            Id = int.Parse(SourceSupportingReferenceId),
            SharedResource = supportingSharedResource,
            SharedResourceId = supportingSharedResource.Id,
            Book = "GEN",
            Chapter = 1,
            Verses = "1"
        };

        using MemoryStream stream = new();
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "SILTranscriber", DateTime.UtcNow.ToString("o"));
            AddJsonEntry(archive, "data/B_organizations.json", new[] { sourceOrganization, extraOrganization }, serviceProvider);
            AddJsonEntry(archive, "data/C_artifactcategorys.json", new[] { supportingCategory }, serviceProvider);
            AddJsonEntry(archive, "data/D_projects.json", new[] { sourceProject }, serviceProvider);
            AddJsonEntry(archive, "data/E_plans.json", new[] { sourcePlan }, serviceProvider);
            AddJsonEntry(archive, "data/F_sections.json", new[] { sourceSection }, serviceProvider);
            AddJsonEntry(archive, "data/G_passages.json", new[] { sourcePrimaryPassage, sourceSupportingPassage }, serviceProvider);
            AddJsonEntry(archive, "data/I_sharedresources.json", new[] { supportingSharedResource }, serviceProvider);
            AddJsonEntry(archive, "data/J_sharedresourcereferences.json", new[] { supportingReference }, serviceProvider);
        }
        return stream.ToArray();
    }

    private static void SeedResumedImportState(AppDbContext dbContext, int targetOrganizationId, string mapKey)
    {
        Project project = new()
        {
            Name = "Archive Source Project",
            OrganizationId = targetOrganizationId,
            ProjecttypeId = 1,
            OwnerId = 1,
            GroupId = dbContext.Groups.Single(group => group.OwnerId == targetOrganizationId && group.AllUsers).Id
        };
        dbContext.Projects.Add(project);
        dbContext.SaveChanges();

        Plan plan = new()
        {
            Name = "Archive Source Project",
            ProjectId = project.Id,
            PlantypeId = 1,
            OwnerId = 1,
            Flat = false,
            SectionCount = 1
        };
        dbContext.Plans.Add(plan);
        dbContext.SaveChanges();

        Section section = new()
        {
            Name = "Archive Section",
            PlanId = plan.Id,
            Sequencenum = 1,
            Level = 1,
            Published = true,
            State = "assigned",
            PublishTo = "{}",
            OfflineId = SourceSectionId
        };
        dbContext.Sections.Add(section);
        dbContext.SaveChanges();

        Passage passage = new()
        {
            Title = "Imported Passage",
            Book = "GEN",
            Reference = "1:1",
            State = "approved",
            Sequencenum = 1,
            SectionId = section.Id,
            PassagetypeId = 1,
            OfflineId = SourcePrimaryPassageId,
            OfflineSharedResourceId = SourceSupportingSharedResourceId
        };
        dbContext.Passages.Add(passage);
        dbContext.SaveChanges();

        Artifactcategory supportingCategory = new()
        {
            Categoryname = "Supporting Note Category",
            Note = true,
            Resource = true,
            Discussion = false,
            OrganizationId = targetOrganizationId
        };
        dbContext.Artifactcategorys.Add(supportingCategory);
        dbContext.SaveChanges();

        dbContext.Copyprojects.AddRange(
            new CopyProject { Sourcetable = Tables.Organizations, Newprojid = mapKey, Oldid = SourceOrganizationId, Newid = targetOrganizationId },
            new CopyProject { Sourcetable = Tables.Organizations, Newprojid = mapKey, Oldid = ExtraOrganizationId, Newid = targetOrganizationId },
            new CopyProject { Sourcetable = Tables.Projects, Newprojid = mapKey, Oldid = SourceProjectId, Newid = project.Id },
            new CopyProject { Sourcetable = Tables.Plans, Newprojid = mapKey, Oldid = SourcePlanId, Newid = plan.Id },
            new CopyProject { Sourcetable = Tables.Sections, Newprojid = mapKey, Oldid = SourceSectionId, Newid = section.Id },
            new CopyProject { Sourcetable = Tables.Passages, Newprojid = mapKey, Oldid = SourcePrimaryPassageId, Newid = passage.Id },
            new CopyProject { Sourcetable = Tables.Passages, Newprojid = mapKey, Oldid = SourceSupportingPassageId, Newid = -1 },
            new CopyProject { Sourcetable = Tables.ArtifactCategorys, Newprojid = mapKey, Oldid = SourceSupportingCategoryId, Newid = supportingCategory.Id });
    }

    private static void SeedResumedImportStateForBatch(AppDbContext dbContext, int targetOrganizationId, string mapKey, int sharedResourceCount)
    {
        Project project = new()
        {
            Name = "Archive Source Project",
            OrganizationId = targetOrganizationId,
            ProjecttypeId = 1,
            OwnerId = 1,
            GroupId = dbContext.Groups.Single(group => group.OwnerId == targetOrganizationId && group.AllUsers).Id
        };
        dbContext.Projects.Add(project);
        dbContext.SaveChanges();

        Plan plan = new()
        {
            Name = "Archive Source Project",
            ProjectId = project.Id,
            PlantypeId = 1,
            OwnerId = 1,
            Flat = false,
            SectionCount = 1
        };
        dbContext.Plans.Add(plan);
        dbContext.SaveChanges();

        Section section = new()
        {
            Name = "Archive Section",
            PlanId = plan.Id,
            Sequencenum = 1,
            Level = 1,
            Published = true,
            State = "assigned",
            PublishTo = "{}",
            OfflineId = SourceSectionId
        };
        dbContext.Sections.Add(section);
        dbContext.SaveChanges();

        List<Passage> passages = [];
        for (int i = 0; i < sharedResourceCount; i++)
        {
            passages.Add(new Passage
            {
                Title = $"Batch Imported Passage {i}",
                Book = "GEN",
                Reference = "1:1",
                State = "approved",
                Sequencenum = i + 1,
                SectionId = section.Id,
                PassagetypeId = 1,
                OfflineId = (BatchSourcePassageBaseId + i).ToString()
            });
        }
        dbContext.Passages.AddRange(passages);
        dbContext.SaveChanges();

        List<CopyProject> maps =
        [
            new CopyProject { Sourcetable = Tables.Organizations, Newprojid = mapKey, Oldid = SourceOrganizationId, Newid = targetOrganizationId },
            new CopyProject { Sourcetable = Tables.Projects, Newprojid = mapKey, Oldid = SourceProjectId, Newid = project.Id },
            new CopyProject { Sourcetable = Tables.Plans, Newprojid = mapKey, Oldid = SourcePlanId, Newid = plan.Id },
            new CopyProject { Sourcetable = Tables.Sections, Newprojid = mapKey, Oldid = SourceSectionId, Newid = section.Id }
        ];

        maps.AddRange(passages.Select((passage, index) => new CopyProject
        {
            Sourcetable = Tables.Passages,
            Newprojid = mapKey,
            Oldid = (BatchSourcePassageBaseId + index).ToString(),
            Newid = passage.Id
        }));

        dbContext.Copyprojects.AddRange(maps);
    }

    private static void SeedResumedImportStateForSectionsBatch(AppDbContext dbContext, int targetOrganizationId, string mapKey)
    {
        Project project = new()
        {
            Name = "Archive Source Project",
            OrganizationId = targetOrganizationId,
            ProjecttypeId = 1,
            OwnerId = 1,
            GroupId = dbContext.Groups.Single(group => group.OwnerId == targetOrganizationId && group.AllUsers).Id
        };
        dbContext.Projects.Add(project);
        dbContext.SaveChanges();

        Plan plan = new()
        {
            Name = "Archive Source Project",
            ProjectId = project.Id,
            PlantypeId = 1,
            OwnerId = 1,
            Flat = false,
            SectionCount = 1
        };
        dbContext.Plans.Add(plan);
        dbContext.SaveChanges();

        Section section = new()
        {
            Name = "Pre-imported Section",
            PlanId = plan.Id,
            Sequencenum = 1,
            Level = 1,
            Published = true,
            State = "assigned",
            PublishTo = "{}",
            OfflineId = BatchSectionBaseId.ToString()
        };
        dbContext.Sections.Add(section);
        dbContext.SaveChanges();

        dbContext.Copyprojects.AddRange(
            new CopyProject { Sourcetable = Tables.Organizations, Newprojid = mapKey, Oldid = SourceOrganizationId, Newid = targetOrganizationId },
            new CopyProject { Sourcetable = Tables.Projects, Newprojid = mapKey, Oldid = SourceProjectId, Newid = project.Id },
            new CopyProject { Sourcetable = Tables.Plans, Newprojid = mapKey, Oldid = SourcePlanId, Newid = plan.Id },
            new CopyProject { Sourcetable = Tables.Sections, Newprojid = mapKey, Oldid = BatchSectionBaseId.ToString(), Newid = section.Id });
    }

    private static ImportedSupportingNoteState LoadImportedSupportingNoteState(AppDbContext dbContext, string mapKey)
    {
        int projectId = dbContext.Copyprojects.Single(cp => cp.Newprojid == mapKey && cp.Sourcetable == Tables.Projects && cp.Oldid == SourceProjectId).Newid;
        Passage importedPassage = dbContext.Passages
            .Include(passage => passage.Section)
            .ThenInclude(section => section!.Plan)
            .Single(passage => passage.OfflineId == SourcePrimaryPassageId && passage.Section != null && passage.Section.PlanId > 0 && passage.Section.Plan!.ProjectId == projectId);
        Sharedresource supportingResource = dbContext.Sharedresources
            .Include(resource => resource.Passage)
            .ThenInclude(passage => passage!.Section)
            .ThenInclude(section => section!.Plan)
            .Single(resource => resource.Title == "Supporting Note" && resource.Passage != null && resource.Passage.Section != null && resource.Passage.Section.Plan != null && resource.Passage.Section.Plan.ProjectId == projectId);
        Sharedresourcereference supportingReference = dbContext.Sharedresourcereferences.Single(reference => reference.SharedResourceId == supportingResource.Id);

        Assert.Equal(importedPassage.Id, supportingResource.PassageId);
        Assert.Null(importedPassage.SharedResourceId);
        Assert.Null(importedPassage.OfflineSharedResourceId);
        Assert.Equal(projectId, dbContext.Plans.Where(p => p.Id == importedPassage.Section!.PlanId).Select(p => p.ProjectId).Single());
        Assert.NotNull(supportingResource.ArtifactCategoryId);
        Artifactcategory supportingCategory = dbContext.Artifactcategorys.Single(category => category.Id == supportingResource.ArtifactCategoryId!.Value);
        Assert.Equal("Supporting Note Category", supportingCategory.Categoryname);
        Assert.Equal(dbContext.Projects.Where(p => p.Id == projectId).Select(p => p.OrganizationId).Single(), supportingCategory.OrganizationId);

        return new ImportedSupportingNoteState(
            projectId,
            supportingResource.Title ?? string.Empty,
            supportingReference.Book,
            supportingReference.Chapter,
            supportingReference.Verses ?? string.Empty,
            supportingCategory.Categoryname ?? string.Empty,
            dbContext.Passages.Count(passage => passage.Section != null && passage.Section.Plan != null && passage.Section.Plan.ProjectId == projectId),
            dbContext.Sharedresources.Count(resource => resource.Passage != null && resource.Passage.Section != null && resource.Passage.Section.Plan != null && resource.Passage.Section.Plan.ProjectId == projectId),
            dbContext.Sharedresourcereferences.Count(reference => dbContext.Sharedresources.Any(resource => resource.Id == reference.SharedResourceId && resource.Passage != null && resource.Passage.Section != null && resource.Passage.Section.Plan != null && resource.Passage.Section.Plan.ProjectId == projectId)));
    }

    private static ZipArchive OpenArchive(byte[] archiveBytes)
    {
        return new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read);
    }

    private static int GetDataEntryIndex(byte[] archiveBytes, string entryName)
    {
        using ZipArchive archive = OpenArchive(archiveBytes);
        return archive.Entries
            .Where(entry => entry.FullName.StartsWith("data", StringComparison.Ordinal))
            .OrderBy(entry => entry.Name)
            .Select(entry => entry.Name)
            .ToList()
            .IndexOf(entryName);
    }

    private static HashSet<string> GetEntryIds(byte[] archiveBytes, string entryName)
    {
        using ZipArchive archive = OpenArchive(archiveBytes);
        ZipArchiveEntry? entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.Name, entryName, StringComparison.OrdinalIgnoreCase)
            || e.FullName.EndsWith(entryName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return [];
        using StreamReader reader = new(entry.Open());
        JObject payload = JObject.Parse(reader.ReadToEnd());
        JToken? data = payload["data"];
        if (data is not JArray array)
            return [];
        return [.. array
            .Select(item => item["id"]?.ToString())
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)];
    }

    private static void AddJsonEntry<TResource>(ZipArchive archive, string entryName, IEnumerable<TResource> resources, IServiceProvider serviceProvider)
        where TResource : class, IIdentifiable
    {
        string json = SerializerHelpers.ResourceListToJson(
            resources,
            serviceProvider.GetRequiredService<IResourceGraph>(),
            serviceProvider.GetRequiredService<IJsonApiOptions>(),
            serviceProvider.GetRequiredService<IResourceDefinitionAccessor>(),
            serviceProvider.GetRequiredService<IMetaBuilder>());
        if (json.Contains("included", StringComparison.Ordinal))
        {
            JObject document = JObject.Parse(json);
            document.Remove("included");
            json = document.ToString();
        }
        WriteEntry(archive, entryName, json);
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
        using StreamWriter writer = new(entry.Open());
        writer.Write(contents);
    }

    private sealed record ImportedSupportingNoteState(
        int ProjectId,
        string SupportingResourceTitle,
        string SupportingReferenceBook,
        int SupportingReferenceChapter,
        string SupportingReferenceVerses,
        string SupportingCategoryName,
        int ImportedPassageCount,
        int SharedResourceCount,
        int SharedResourceReferenceCount);

    private sealed record ExportFixture(
        int ProjectId,
        int GlobalCategoryId,
        int PrimaryCategoryId,
        int ForeignUsedCategoryId,
        int ForeignUnusedCategoryId,
        int ForeignUsedTitleMediaId,
        int ForeignUnusedTitleMediaId);

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public const string TestAuth0Id = "auth0|offline-data-service-tests";

        public string Auth0Id => TestAuth0Id;
        public string Email => "test@example.com";
        public string GivenName => "Test";
        public string FamilyName => "User";
        public string Name => "Test User";
        public string Avatar => string.Empty;
        public bool EmailVerified => true;
        public UserSecret? ParatextLogin(string connection, int userId) => null;
        public UserSecret? ParatextToken(Identity ptIdentity, int userId) => null;
        public UserSecret? ParatextToken(JToken ptIdentity, int id) => null;
    }

    private sealed class FakeSqsService : ISQSService
    {
        public Task<int> MessageCount(string queue) => Task.FromResult(0);
        public Task<int> BBMessageCount() => Task.FromResult(0);
        public string SendBBResourceMessage(string filesetId, string book, int chapter, int? psgId, int sectionId, int planId, string lang, string desc, int startverse, int endverse, int seq, int artifactTypeId, int? artifactCategoryId, int orgWorkflowStepId, string token) => string.Empty;
        public string SendBBGeneralMessage(string filesetId, string? codec, string book, int chapter, int planId, string lang, string desc, int artifactTypeId, int? artifactCategoryId, string token) => string.Empty;
        public string SendExportMessage(int projectId, string folder, string ptfFile, int start) => string.Empty;
        public string SendMessage(string url, string body, string? deDup, string? groupId) => string.Empty;
    }

    private sealed class FakeS3Service : IS3Service
    {
        private readonly Dictionary<string, byte[]> _objects = [];

        public byte[] GetFileBytes(string folder, string fileName)
        {
            return _objects[$"{folder}/{fileName}"];
        }

        public Task<S3Response> CreateBucketAsync(string bucketName) => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> UploadFileAsync(Stream stream, bool overwriteifExists, string fileName, string folder = "", string bucket = "")
        {
            using MemoryStream copy = new();
            stream.Position = 0;
            stream.CopyTo(copy);
            _objects[$"{folder}/{fileName}"] = copy.ToArray();
            return Task.FromResult(new S3Response { Status = HttpStatusCode.OK, Message = fileName });
        }
        public Task<S3Response> CopyFile(string fileName, string newFileName, string folder = "", string newFolder = "")
        {
            _objects[$"{newFolder}/{newFileName}"] = _objects[$"{folder}/{fileName}"];
            return Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        }
        public Task<HttpStatusCode> CopyS3FileAsync(string sourceFileUrl, string destinationBucket, string folder, string filename) => Task.FromResult(HttpStatusCode.OK);
        public Task<HttpStatusCode> CopyS3FileAsync(string sourceFileUrl, string folder, string filename) => Task.FromResult(HttpStatusCode.OK);
        public Task<S3Response> RenameFile(string fileName, string newFileName, string folder = "")
        {
            _objects[$"{folder}/{newFileName}"] = _objects[$"{folder}/{fileName}"];
            _objects.Remove($"{folder}/{fileName}");
            return Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        }
        public Task<S3Response> RemoveFile(string fileName, string folder = "", string bucket = "")
        {
            _objects.Remove($"{folder}/{fileName}");
            return Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        }
        public Task<S3Response> ListObjectsAsync(string folder = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> ReadObjectDataAsync(string keyName, string folder = "", bool forWrite = false)
        {
            _objects.TryGetValue($"{folder}/{keyName}", out byte[]? bytes);
            return Task.FromResult(new S3Response
            {
                Status = HttpStatusCode.OK,
                Message = DateTime.UtcNow.ToString("o"),
                FileStream = bytes == null ? null : new MemoryStream(bytes)
            });
        }
        public Task<bool> FileExistsAsync(string fileName, string folder = "", string bucket = "") => Task.FromResult(_objects.ContainsKey($"{folder}/{fileName}"));
        public S3Response SignedUrlForGet(string fileName, string folder, string contentType) => new() { Status = HttpStatusCode.OK, Message = fileName };
        public S3Response SignedUrlForPut(string fileName, string folder, string contentType, string bucket = "", string accesskey = "", string secret = "") => new() { Status = HttpStatusCode.OK, Message = fileName };
        public Task<string> GetFilename(string folder, string filename, bool overwrite = false, string suffix = "") => Task.FromResult(filename);
        public Task<S3Response> MakePublic(string fileName, string folder = "", string bucket = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> BucketOwner(string fileName, string folder = "", string bucket = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public string GetPublicUrl(string fileName, string folder = "", string bucket = "") => fileName;
        public Task<MultipartInitiateResponse> InitiateMultipartUploadAsync(string key, string contentType, int parts, string folder, bool aerobucket) => Task.FromResult(new MultipartInitiateResponse());
        public Task<Fileresponse> CompleteMultipartUploadAsync(string key, string uploadId, List<MultipartPartETag> parts) => Task.FromResult(new Fileresponse { Status = HttpStatusCode.OK });
        public Task<Fileresponse> AbortMultipartUploadAsync(string key, string uploadId) => Task.FromResult(new Fileresponse { Status = HttpStatusCode.OK });
        public Task<Fileresponse> ReplaceMultipartPartAsync(string uploadId, int part, string key, string folder = "") => Task.FromResult(new Fileresponse { Status = HttpStatusCode.OK });
    }
}
