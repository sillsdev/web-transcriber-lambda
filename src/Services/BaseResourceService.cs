using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.Net;

namespace SIL.Transcriber.Services;

public class BaseResourceService
{
    protected readonly AppDbContext DbContext;
    protected readonly IS3Service S3service;
    protected readonly HttpClient Client = new();
    public BaseResourceService(
       AppDbContextResolver contextResolver,
       IS3Service s3Service)
    {
        DbContext = (AppDbContext)contextResolver.GetContext();
        S3service = s3Service;
    }
    protected Mediafile CreateMedia(string originalFile, string contentType, string desc, int? passageId, int planId,
                                int artifacttypeId, string lang, string s3file, string folder, int? artifactcategoryId = null, int? sourceMediaId=null, string? segments= "{}")
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Mediafile> m =
                            DbContext.Mediafiles.Add(new Mediafile
                            {
                                ContentType = contentType,
                                OriginalFile = originalFile,
                                Topic = desc,
                                PassageId = passageId,
                                PlanId = planId,
                                ArtifactTypeId = artifacttypeId,
                                VersionNumber = 1,
                                PublishTo = "{}",
                                Languagebcp47 = lang,
                                S3File = s3file,
                                S3Folder = folder,
                                Link = false,
                                AudioUrl =  S3service.SignedUrlForPut(originalFile, folder, contentType).Message,
                                ArtifactCategoryId = artifactcategoryId,
                                SourceMediaId = sourceMediaId,
                                Segments=segments,
                            }); 
        DbContext.SaveChanges();
        return m.Entity;
    }
    protected Sectionresource CreateSR(string desc, int seq, int mediafileId, int sectionId, int? passageId, int orgWorkflowStepId)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Sectionresource> sr =
            DbContext.Sectionresources.Add(new Sectionresource
            {
                Description = desc,
                SequenceNum = seq,
                MediafileId = mediafileId,
                SectionId = sectionId,
                PassageId = passageId,
                OrgWorkflowStepId = orgWorkflowStepId
            });
        DbContext.SaveChanges();
        return sr.Entity;
    }
    protected async Task<string> UrlToS3(string url, string folder)
    {
        Uri uri = new (url);
        string fileName = Path.GetFileName(uri.LocalPath);
        using Stream responseStream = await Client.GetStreamAsync(uri);
        using MemoryStream tempFile = new();

        await responseStream.CopyToAsync(tempFile);
        tempFile.Seek(0, SeekOrigin.Begin);

        S3Response s3 = await S3service.UploadFileAsync(tempFile, true, fileName, folder);
        return s3.Status == HttpStatusCode.OK ? s3.Message : throw new Exception($"Error uploading to S3: {s3.Message}");
    }

}
