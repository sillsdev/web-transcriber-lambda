using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface IS3Service
    {
        Task<S3Response> CreateBucketAsync(string bucketName);
        Task<S3Response> UploadFileAsync(IFormFile file, string folder = "");
        Task<S3Response> UploadFileAsync(Stream stream, bool overwriteifExists, string ContentType, string fileName, string folder = "");
        Task<S3Response> RemoveFile(string fileName, string folder = "");
        Task<S3Response> ListObjectsAsync(string folder = "");
        Task<S3Response> ReadObjectDataAsync(string keyName, string folder = "");
        Task<bool> FileExistsAsync(string fileName, string folder = "");
        S3Response SignedUrlForGet(string fileName, string folder, string contentType);
        S3Response SignedUrlForPut(string fileName, string folder, string contentType);
    }
}
