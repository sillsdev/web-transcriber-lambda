﻿using SIL.Transcriber.Models;
using System.Net;

namespace SIL.Transcriber.Services
{
    public interface IS3Service
    {
        Task<S3Response> CreateBucketAsync(string bucketName);
        Task<S3Response> UploadFileAsync(Stream stream, bool overwriteifExists, string fileName, string folder = "", string bucket = "");
        Task<S3Response> CopyFile(string fileName, string newFileName, string folder = "", string newFolder = "");
        Task<HttpStatusCode> CopyS3FileAsync(string sourceFileUrl, string destinationBucket, string destinationKey);
        Task<S3Response> RenameFile(string fileName, string newFileName, string folder = "");
        Task<S3Response> RemoveFile(string fileName, string folder = "", string bucket = "");
        Task<S3Response> ListObjectsAsync(string folder = "");
        Task<S3Response> ReadObjectDataAsync(string keyName, string folder = "", bool forWrite = false);
        Task<bool> FileExistsAsync(string fileName, string folder = "", string bucket = "");
        S3Response SignedUrlForGet(string fileName, string folder, string contentType);
        S3Response SignedUrlForPut(string fileName, string folder, string contentType, string bucket = "", string accesskey = "", string secret = "");
        Task<string> GetFilename(string folder, string filename, bool overwrite = false, string suffix = "");
        Task<S3Response> MakePublic(string fileName, string folder = "", string bucket = "");
        Task<S3Response> BucketOwner(string fileName, string folder = "", string bucket = "");
        string GetPublicUrl(string fileName, string folder = "", string bucket = "");
        Task<S3Response> CreatePublishRequest(int id, string inputKey, string outputKey, string tags);






    }
}
