using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public interface IS3Service
    {
        Task<S3Response> CreateBucketAsync(string bucketName);
        Task<S3Response> UploadFileAsync(Stream stream, bool overwriteifExists, string fileName, string folder = "", bool userfile = true);
        Task<S3Response> CopyFile(string fileName, string newFileName, string folder = "", string newFolder = "");
        Task<S3Response> RenameFile(string fileName, string newFileName, string folder = "");
        Task<S3Response> RemoveFile(string fileName, string folder = "", bool userfile = true);
        Task<S3Response> ListObjectsAsync(string folder = "");
        Task<S3Response> ReadObjectDataAsync(string keyName, string folder = "", bool forWrite = false);
        Task<bool> FileExistsAsync(string fileName, string folder = "",bool userfile = true);
        S3Response SignedUrlForGet(string fileName, string folder, string contentType);
        S3Response SignedUrlForPut(string fileName, string folder, string contentType);
        Task<string> GetFilename(string folder, string filename, bool overwrite = false, string suffix = "");
        Task<S3Response> MakePublic(string fileName, string folder = "", bool userfile = true);
        string GetPublicUrl(string fileName, string folder = "", bool userfile = true);
        Task<S3Response> CreatePublishRequest(int id, string inputKey, string outputKey, string tags);






        }
    }
