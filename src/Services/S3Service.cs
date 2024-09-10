using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3.Util;
using Auth0.ManagementApi.Models;
using SIL.Transcriber.Models;
using System.Net;
using System.Text;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static System.Net.Mime.MediaTypeNames;

namespace SIL.Transcriber.Services
{
    public class S3WrapperStream : Stream
    {
        readonly IAmazonS3 _s3;
        readonly string _bucket;
        readonly string _key;
        readonly long _length;
        private long _offset;
        protected ILogger<S3Service> Logger { get; set; }

        // Keep a local buffer to avoid many small round trips to S3
        // Typical sizes for byte-range requests are 8 MB or 16 MB. 
        readonly byte[] _localBuffer = new byte[1024*16];
        long _localStart = 0;
        long _localLength = 0;

        public S3WrapperStream(ILogger<S3Service> logger, IAmazonS3 s3, string bucket, string key)
        {
            Logger = logger;
            _s3 = s3;
            _bucket = bucket;
            _key = key;
            _offset = 0;

            // Get the object size to enable Seek from end operations
            GetObjectMetadataResponse data = _s3.GetObjectMetadataAsync(bucket, key).Result;
            _length = data.ContentLength;
        }

        // Implementations of Stream's properties
        public override bool CanSeek => true;
        public override bool CanRead => true;
        public override long Length => _length;
        public override long Position { get => _offset; set => _offset = value; }

        // Seek simply moves our current pointer around
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _offset = offset;
                    break;
                case SeekOrigin.End:
                    _offset = _length + offset;
                    break;
                case SeekOrigin.Current:
                    _offset += offset;
                    break;
            }
            return _offset;
        }

        // Turn reads into S3 calls
        public override int Read(byte [] buffer, int offset, int count)
        {
            //Logger.LogInformation("S3WrapperStream Read from {o} len {c} bufstart {ls} buflen {lc}", _offset, count, _localStart, _localLength);

            if (count > _localBuffer.Length)
            {
                // A big read goes directly to S3
                GetObjectRequest req = new() 
                {
                    BucketName = _bucket,
                    Key = _key,
                    ByteRange = new ByteRange(_offset, _offset + count - 1),
                };
                Logger.LogInformation("S3WrapperStream Big Read {o} {s}", _offset, offset);

                GetObjectResponse resp = _s3.GetObjectAsync(req).Result;
                int read = resp.ResponseStream.Read(buffer, offset, count);
                while (read < count && resp.HttpStatusCode == HttpStatusCode.PartialContent)
                {
                    // We didn't get enough data to fill the request, so we're at the end of the file
                    Logger.LogInformation("partial content {r} {c}", read, count);
                    read += resp.ResponseStream.ReadAsync(buffer, offset+read, count - read).Result;
                }
                _offset += read;
                return read;
            }
            else
            {
                // Otherwise, the read is small enough to fit in our local buffer
                if (_offset < _localStart || (_offset + count) >= _localStart + _localLength)
                {
                    // A request for data outside of our buffer came in, fill up to the size
                    // of our buffer first
                    long end = Math.Min(_length, _offset + _localBuffer.Length - 1);
                    GetObjectRequest req = new ()
                    {
                        BucketName = _bucket,
                        Key = _key,
                        ByteRange = new ByteRange(_offset, end ),
                    };
                    GetObjectResponse resp = _s3.GetObjectAsync(req).Result;
                    int read = resp.ResponseStream.ReadAsync(_localBuffer, 0, _localBuffer.Length).Result;
                    while (read < count && resp.HttpStatusCode == HttpStatusCode.PartialContent)
                    {
                        // We didn't get enough data to fill the request, so we're at the end of the file
                        Logger.LogInformation("partial content {r} {c}", read, count);
                        read += resp.ResponseStream.ReadAsync(_localBuffer, read, _localBuffer.Length-read).Result;
                    }
                    Logger.LogInformation("S3WrapperStream Fill Buffer offset {s} {e} {cl} returned {r}", req.ByteRange.Start, req.ByteRange.End, resp.ContentLength, read);
                    _localStart = _offset;
                    _localLength = read;
                }
                // The data is in our buffer, pull out the correct data and return it
                Buffer.BlockCopy(_localBuffer, (int)(_offset - _localStart), buffer, offset, count);
                _offset += count;
                return count;
            }
        }

        // No need to implement write methods
        public override bool CanWrite => false;
        public override void Flush() { throw new NotImplementedException(); }
        public override void SetLength(long value) { throw new NotImplementedException(); }
        public override void Write(byte [] buffer, int offset, int count) { throw new NotImplementedException(); }
    }

    public class S3Service : IS3Service
    {
        private readonly string USERFILES_BUCKET;
        private readonly string PUBLISHREQ_BUCKET;
        private readonly string PUBLISHED_BUCKET;
        private readonly IAmazonS3 _client;
        protected ILogger<S3Service> Logger { get; set; }

        public S3Service(IAmazonS3 client, ILoggerFactory loggerFactory)
        {
            _client = client;
            USERFILES_BUCKET = GetVarOrThrow("SIL_TR_USERFILES_BUCKET");
            PUBLISHREQ_BUCKET = GetVarOrThrow("SIL_TR_PUBLISHREQ_BUCKET");
            PUBLISHED_BUCKET = GetVarOrThrow("SIL_TR_PUBLISHED_BUCKET");
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
        private long GetFileSize(string key)
        {
            // Get the object size to enable Seek from end operations
            GetObjectMetadataResponse data = GetFileData(key);
            return data.ContentLength;
        }
        private GetObjectMetadataResponse GetFileData(string key)
        {
            GetObjectMetadataResponse data = _client.GetObjectMetadataAsync(USERFILES_BUCKET, key).Result;
            return data;
        }
        public async Task<bool> FileExistsAsync(string fileName, string folder = "",
            bool userfile = true)
        {
            fileName = ProperFolder(folder) + fileName;
            ListObjectsResponse response = await _client.ListObjectsAsync(
                userfile ? USERFILES_BUCKET : PUBLISHREQ_BUCKET,
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
        public async Task<string> GetFilename (string folder, string filename, bool overwrite = false, string suffix = "")
        {
           string ext = Path.GetExtension(filename)??"";
           string newfilename = Path.GetFileNameWithoutExtension(filename) +suffix + ext;
           return !overwrite && await FileExistsAsync(newfilename, folder)
                ? Path.GetFileNameWithoutExtension(filename)
                    + "__"
                    + Guid.NewGuid()
                    + suffix
                    + ext
                : newfilename;
        }
        public async Task<S3Response> CreatePublishRequest(int id, string inputKey, string outputKey) { 
            try
            {
                string json = $"{{\"id\":{id},\"inputBucket\":\"{USERFILES_BUCKET}\",\"inputKey\":\"{inputKey}\",\"outputBucket\":\"{PUBLISHED_BUCKET}\",\"outputKey\":\"{outputKey}\"}}";
                string requestKey = id + ".key";
                using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                return await UploadFileAsync(stream, true, requestKey, "", false);
            }
            catch (Exception e)
            {
                return S3Response(e.Message, HttpStatusCode.InternalServerError);
            }
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
        public async Task<S3Response> MakePublic(string fileName, string folder = "", bool userfile = true)
        {
            try
            {
                PutACLRequest request = new()
                {
                    BucketName = userfile ? USERFILES_BUCKET : PUBLISHREQ_BUCKET,
                    Key = ProperFolder(folder) + fileName,
                    CannedACL = S3CannedACL.PublicRead,
                };
                PutACLResponse response = await _client.PutACLAsync(request);
                return S3Response("", response.HttpStatusCode);
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
        public string GetPublicUrl(string fileName, string folder = "", bool userfile = true)
        {
            return $"https://{(userfile ? USERFILES_BUCKET : PUBLISHREQ_BUCKET)}.s3.amazonaws.com/{ProperFolder(folder)}{fileName}";
        }
        public async Task<S3Response> UploadFileAsync(
            Stream stream,
            bool overwriteifExists,
            string fileName,
            string folder = "",
            bool userfile = true
        )
        {
            try
            {
                if (overwriteifExists && await FileExistsAsync(fileName, folder,userfile))
                {
                    _ = await RemoveFile(fileName, folder, userfile);
                }
                TransferUtility fileTransferUtility = new(_client);
                await fileTransferUtility.UploadAsync(
                    stream,
                    userfile ? USERFILES_BUCKET : PUBLISHREQ_BUCKET,
                    ProperFolder(folder) + fileName
                );

                return new S3Response
                {
                    Message = fileName,
                    Status = HttpStatusCode.OK,
                    FileURL = GetPublicUrl(fileName,folder,userfile)
                };
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

        public async Task<S3Response> RemoveFile(string fileName, string folder = "", bool userfile = true)
        {
            //var client = new AmazonS3Client(accessKey, accessSecret, Amazon.RegionEndpoint.EUCentral1);
            try
            {
                //check if it exists
                //check if file with metadata OriginalFileName = fileName exists
                DeleteObjectRequest request =
                    new() { BucketName = userfile ? USERFILES_BUCKET : PUBLISHREQ_BUCKET,
                        Key = ProperFolder(folder) + fileName, };

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
        public async Task<S3Response> ReadObjectDataAsync(string fileName, string folder = "", bool forWrite = false)
        {
            try
            {
                const long limit = 1024*1024*512; //512MB;
                string key = ProperFolder(folder) + fileName;
                GetObjectMetadataResponse filedata = GetFileData(key);

                Stream stream;
                //decide if I need to wrap the stream in a S3WrapperStream
                if (!forWrite && filedata.ContentLength > limit)
                    stream = new S3WrapperStream(Logger, _client, USERFILES_BUCKET, key);
                else
                {
                    stream = new MemoryStream();
                    //read it into memory
                    GetObjectRequest request =
                        new() { BucketName = USERFILES_BUCKET, Key = key };
                    GetObjectResponse response = await _client.GetObjectAsync(request);
                    await response.ResponseStream.CopyToAsync(stream);
                }
                stream.Position = 0;
                return S3Response(
                        filedata.LastModified.ToString(),
                        HttpStatusCode.OK,
                        stream,
                        filedata.Headers["ContentType"]);
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
