using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using SIL.Transcriber.Models;
using System.Net;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    public class S3Service : IS3Service
    {
        private readonly string USERFILES_BUCKET;
        private readonly IAmazonS3 _client;
        protected ILogger<S3Service> Logger { get; set; }

        public S3Service(IAmazonS3 client, ILoggerFactory loggerFactory)
        {
            _client = client;
            USERFILES_BUCKET = GetVarOrThrow("SIL_TR_USERFILES_BUCKET");
            this.Logger = loggerFactory.CreateLogger<S3Service>();
        }

        private static string ProperFolder(string folder)
        {
            //what else should be checked here?
            if (folder.Length > 0 && folder.LastIndexOf("/") != folder.Length - 1)
                folder += "/";
            return folder;
        }

        private static S3Response S3Response(
            string message,
            HttpStatusCode code,
            Stream? fileStream = null,
            string contentType = ""
        )
        {
            return new S3Response
            {
                Message = message,
                Status = code,
                FileStream = fileStream,
                ContentType = contentType,
            };
        }

        public async Task<bool> FileExistsAsync(string fileName, string folder = "")
        {
            fileName = ProperFolder(folder) + fileName;
            ListObjectsResponse response = await _client.ListObjectsAsync(
                USERFILES_BUCKET,
                fileName
            );
            //ListObjects uses the passed in filename as a prefix ie. filename*, so check if we have an exact match
            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                for (int o = 0; o < response.S3Objects.Count; o++)
                {
                    if (response.S3Objects [o].Key == fileName)
                        return true;
                }
            }
            else
            {
                Console.WriteLine("FileExistsAsync error:" + response.HttpStatusCode.ToString());
                return false;
            }
            return false;
        }

        public async Task<S3Response> CreateBucketAsync(string bucketName)
        {
            try
            {
                if (await AmazonS3Util.DoesS3BucketExistV2Async(_client, bucketName) == false)
                {
                    PutBucketRequest putBucketRequest =
                        new() { BucketName = bucketName, UseClientRegion = true };
                    PutBucketResponse response = await _client.PutBucketAsync(putBucketRequest);
                    return S3Response(response.ResponseMetadata.RequestId, response.HttpStatusCode);
                }
                else
                    return S3Response(
                        "The requested bucket name is not available. The bucket namespace is shared by all users of the system. Please select a different name and try again.",
                        HttpStatusCode.Conflict
                    );
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        private string SignedUrl(string key, HttpVerb action, string mimetype)
        {
            AmazonS3Client s3Client = new();

            GetPreSignedUrlRequest request =
                new()
                {
                    BucketName = USERFILES_BUCKET,
                    Key = key,
                    Verb = action,
                    Expires = DateTime.Now.AddMinutes(25),
                };
            if (mimetype.Length > 0)
                request.ContentType = mimetype;
            try
            {
                return s3Client.GetPreSignedURL(request);
            }
            catch (WebException e)
            {
                Console.Write(e.Status);
                throw;
            }
        }

        public S3Response SignedUrlForGet(string fileName, string folder, string contentType)
        {
            try
            {
                return S3Response(
                    SignedUrl(ProperFolder(folder) + fileName, HttpVerb.GET, ""),
                    HttpStatusCode.OK
                );
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public S3Response SignedUrlForPut(string fileName, string folder, string contentType)
        {
            try
            {
                string url = SignedUrl(ProperFolder(folder) + fileName, HttpVerb.PUT, contentType);
                return S3Response(url, HttpStatusCode.OK);
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<S3Response> UploadFileAsync(
            Stream stream,
            bool overwriteifExists,
            string ContentType,
            string fileName,
            string folder = ""
        )
        {
            try
            {
                if (overwriteifExists && await FileExistsAsync(fileName, folder))
                {
                    _ = await RemoveFile(fileName, folder);
                }
                TransferUtility fileTransferUtility = new(_client);
                await fileTransferUtility.UploadAsync(
                    stream,
                    USERFILES_BUCKET,
                    ProperFolder(folder) + fileName
                );
                return new S3Response { Message = fileName, Status = HttpStatusCode.OK, };
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<S3Response> UploadFileAsync(IFormFile file, string folder = "")
        {
            try
            {
                byte[] fileBytes = new byte[file.Length];
                _ = file.OpenReadStream().Read(fileBytes, 0, int.Parse(file.Length.ToString()));

                // create unique file name
                string fileName = Guid.NewGuid() + "_" + file.FileName;
                //aws versioning on
                //var fileName = file.FileName;

                return await UploadFileAsync(
                    new MemoryStream(fileBytes),
                    false,
                    file.ContentType,
                    fileName,
                    folder
                );
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<S3Response> RemoveFile(string fileName, string folder = "")
        {
            //var client = new AmazonS3Client(accessKey, accessSecret, Amazon.RegionEndpoint.EUCentral1);
            try
            {
                //check if it exists
                //check if file with metadata OriginalFileName = fileName exists
                DeleteObjectRequest request =
                    new() { BucketName = USERFILES_BUCKET, Key = ProperFolder(folder) + fileName, };

                DeleteObjectResponse response = await _client.DeleteObjectAsync(request);
                return S3Response(fileName, response.HttpStatusCode);
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }
        public async Task<S3Response> CopyFile(string fileName,
                                               string newFileName,
                                               string folder = "",
                                               string newFolder = "")
        {
            try
            {
                //save it as the newName
                S3Response s3response = ReadObjectDataAsync(fileName, folder).Result;
                if (s3response.FileStream == null)
                {
                    return s3response;
                }

                s3response = await UploadFileAsync(
                    s3response.FileStream,
                    true,
                    s3response.ContentType,
                    newFileName,
                    newFolder
                );
                return S3Response(newFileName, s3response.Status);
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<S3Response> RenameFile(
            string fileName,
            string newFileName,
            string folder = ""
        )
        {
            try
            {
                S3Response s3response = await CopyFile(fileName, newFileName, folder, folder);
                if (s3response.Status == HttpStatusCode.OK)
                    _ = RemoveFile(fileName, folder);
                return S3Response(newFileName, s3response.Status);
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<S3Response> ListObjectsAsync(string folder = "")
        {
            try
            {
                // List the objects in this bucket.
                string list = "";
                ListObjectsResponse response = await _client.ListObjectsAsync(
                    USERFILES_BUCKET,
                    ProperFolder(folder)
                );
                if (response.HttpStatusCode == HttpStatusCode.OK)
                {
                    list = "[";
                    for (int o = 0; o < response.S3Objects.Count; o++)
                    {
                        list += string.Format(
                            "{{\"Key\":\"{0}\",\"Size\":\"{1}\",\"LastModified\":\"{2}\"}},",
                            response.S3Objects [o].Key,
                            response.S3Objects [o].Size,
                            response.S3Objects [o].LastModified
                        );
                    }
                    list += "]";
                }
                return S3Response(list, response.HttpStatusCode, null, "application/json");
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }

        public async Task<S3Response> ReadObjectDataAsync(string fileName, string folder = "")
        {
            try
            {
                GetObjectRequest request =
                    new() { BucketName = USERFILES_BUCKET, Key = ProperFolder(folder) + fileName, };

                using GetObjectResponse response = await _client.GetObjectAsync(request);
                using Stream responseStream = response.ResponseStream;
                MemoryStream stream = new();
                await responseStream.CopyToAsync(stream);
                stream.Position = 0;

                return S3Response(
                    (response.LastModified.AddMinutes(2) > DateTime.UtcNow).ToString(),
                    HttpStatusCode.OK,
                    stream,
                    response.Headers ["Content-Type"]
                );
            }
            catch (AmazonS3Exception e)
            {
                return S3Response(e.Message, e.StatusCode);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
        }
    }
}
