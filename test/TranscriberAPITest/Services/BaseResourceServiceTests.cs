using Amazon.S3.Model;
using Auth0.ManagementApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Paratext.Models;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Services.Contracts;
using System.Net;
using Xunit;

namespace TranscriberAPI.Tests.Services;

public class BaseResourceServiceTests
{
    [Fact]
    public void CreateMedia_WithS3File_UsesSignedGetUrl()
    {
        using AppDbContext dbContext = CreateDbContext();
        TestS3Service s3Service = new();
        TestBaseResourceService service = new(new AppDbContextResolver(dbContext), s3Service);

        Mediafile mediafile = service.CreateMediaForTest(
            "WEB-0056_barley_grain.jpg",
            "image/jpg",
            "Aquifer image",
            534304,
            100,
            200,
            "eng",
            "WEB-0056_barley_grain.jpg",
            "aquifer/");

        Assert.Equal("GET:aquifer/WEB-0056_barley_grain.jpg", mediafile.AudioUrl);
        Assert.Equal("WEB-0056_barley_grain.jpg", mediafile.S3File);
    }

    [Fact]
    public void CreateMedia_WithoutS3File_UsesSignedPutUrl()
    {
        using AppDbContext dbContext = CreateDbContext();
        TestS3Service s3Service = new();
        TestBaseResourceService service = new(new AppDbContextResolver(dbContext), s3Service);

        Mediafile mediafile = service.CreateMediaForTest(
            "# Markdown resource",
            "text/markdown",
            "Aquifer text",
            534304,
            100,
            200,
            "eng",
            "",
            "");

        Assert.Equal("PUT:# Markdown resource", mediafile.AudioUrl);
        Assert.Equal(string.Empty, mediafile.S3File);
    }

    private static AppDbContext CreateDbContext()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"base-resource-service-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options, new TestCurrentUserContext(), new HttpContextAccessor(), LoggerFactory.Create(_ => { }));
    }

    private sealed class TestBaseResourceService(AppDbContextResolver contextResolver, IS3Service s3Service)
        : BaseResourceService(contextResolver, s3Service)
    {
        public Mediafile CreateMediaForTest(string originalFile, string contentType, string desc, int? passageId, int planId,
            int artifactTypeId, string lang, string s3File, string folder)
        {
            return CreateMedia(originalFile, contentType, desc, passageId, planId, artifactTypeId, lang, s3File, folder);
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public string Auth0Id => "auth0|test";
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

    private sealed class TestS3Service : IS3Service
    {
        public Task<S3Response> CreateBucketAsync(string bucketName) => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> UploadFileAsync(Stream stream, bool overwriteifExists, string fileName, string folder = "", string bucket = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK, Message = fileName });
        public Task<S3Response> CopyFile(string fileName, string newFileName, string folder = "", string newFolder = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK, Message = newFileName });
        public Task<HttpStatusCode> CopyS3FileAsync(string sourceFileUrl, string destinationBucket, string folder, string filename) => Task.FromResult(HttpStatusCode.OK);
        public Task<HttpStatusCode> CopyS3FileAsync(string sourceFileUrl, string folder, string filename) => Task.FromResult(HttpStatusCode.OK);
        public Task<S3Response> RenameFile(string fileName, string newFileName, string folder = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK, Message = newFileName });
        public Task<S3Response> RemoveFile(string fileName, string folder = "", string bucket = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> ListObjectsAsync(string folder = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> ReadObjectDataAsync(string keyName, string folder = "", bool forWrite = false) => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<bool> FileExistsAsync(string fileName, string folder = "", string bucket = "") => Task.FromResult(true);
        public S3Response SignedUrlForGet(string fileName, string folder, string contentType) => new() { Status = HttpStatusCode.OK, Message = $"GET:{folder}{fileName}" };
        public S3Response SignedUrlForPut(string fileName, string folder, string contentType, string bucket = "", string accesskey = "", string secret = "") => new() { Status = HttpStatusCode.OK, Message = $"PUT:{fileName}" };
        public Task<string> GetFilename(string folder, string filename, bool overwrite = false, string suffix = "") => Task.FromResult(filename);
        public Task<S3Response> MakePublic(string fileName, string folder = "", string bucket = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public Task<S3Response> BucketOwner(string fileName, string folder = "", string bucket = "") => Task.FromResult(new S3Response { Status = HttpStatusCode.OK });
        public string GetPublicUrl(string fileName, string folder = "", string bucket = "") => $"PUBLIC:{folder}{fileName}";
        public Task<MultipartInitiateResponse> InitiateMultipartUploadAsync(string key, string contentType, int parts, string folder, bool aerobucket) => Task.FromResult(new MultipartInitiateResponse());
        public Task<Fileresponse> CompleteMultipartUploadAsync(string key, string uploadId, List<MultipartPartETag> parts) => Task.FromResult(new Fileresponse { Status = HttpStatusCode.OK });
        public Task<Fileresponse> AbortMultipartUploadAsync(string key, string uploadId) => Task.FromResult(new Fileresponse { Status = HttpStatusCode.OK });
        public Task<Fileresponse> ReplaceMultipartPartAsync(string uploadId, int part, string key, string folder = "") => Task.FromResult(new Fileresponse { Status = HttpStatusCode.OK });
    }
}
