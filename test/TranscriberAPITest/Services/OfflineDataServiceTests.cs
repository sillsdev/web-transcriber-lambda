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
            Assert.DoesNotContain(dbContext.Copyprojects, cp => cp.Newprojid == fullMapKey && cp.Sourcetable == Tables.Organizations && cp.Oldid == ExtraOrganizationId);
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
        }
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
        Sharedresource supportingSharedResource = new()
        {
            Id = int.Parse(SourceSupportingSharedResourceId),
            Title = "Supporting Note",
            Description = "Supporting note owned by a skipped passage",
            Note = true,
            PassageId = int.Parse(SourceSupportingPassageId)
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

        dbContext.Copyprojects.AddRange(
            new CopyProject { Sourcetable = Tables.Organizations, Newprojid = mapKey, Oldid = SourceOrganizationId, Newid = targetOrganizationId },
            new CopyProject { Sourcetable = Tables.Projects, Newprojid = mapKey, Oldid = SourceProjectId, Newid = project.Id },
            new CopyProject { Sourcetable = Tables.Plans, Newprojid = mapKey, Oldid = SourcePlanId, Newid = plan.Id },
            new CopyProject { Sourcetable = Tables.Sections, Newprojid = mapKey, Oldid = SourceSectionId, Newid = section.Id },
            new CopyProject { Sourcetable = Tables.Passages, Newprojid = mapKey, Oldid = SourcePrimaryPassageId, Newid = passage.Id },
            new CopyProject { Sourcetable = Tables.Passages, Newprojid = mapKey, Oldid = SourceSupportingPassageId, Newid = -1 });
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

        return new ImportedSupportingNoteState(
            projectId,
            supportingResource.Title ?? string.Empty,
            supportingReference.Book,
            supportingReference.Chapter,
            supportingReference.Verses ?? string.Empty,
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
        int ImportedPassageCount,
        int SharedResourceCount,
        int SharedResourceReferenceCount);

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
