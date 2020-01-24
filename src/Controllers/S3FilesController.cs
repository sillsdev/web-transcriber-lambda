using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class S3FilesController : ControllerBase
    {
        private readonly IS3Service _service;
        public S3FilesController(IS3Service service)
        {
            _service = service;
        }
        /*
        [HttpPost("{bucketName}")]
        public async Task<IActionResult> CreateBucket([FromRoute] string bucketName)
        {
            var response = await _service.CreateBucketAsync(bucketName);
            return Ok(response);
        }
        */
        [HttpGet]
        public async Task<IActionResult> ListFiles()
        {
            S3Response s3response = await _service.ListObjectsAsync();

            if (s3response.Status == HttpStatusCode.OK)
            {
                return Ok(s3response.Message);
            }

            return Ok(s3response);
        }

        private async Task<IActionResult> GetS3File(string folder, string fileName, string fileNameOut = "")
        {
            S3Response response = await _service.ReadObjectDataAsync(fileName, folder);

            if (response.Status == HttpStatusCode.OK)
            {
                Response.Headers.Add("Content-Disposition", new ContentDisposition
                {
                    FileName = fileNameOut.Length > 0 ? fileNameOut : fileName,
                    Inline = true // false = prompt the user for downloading; true = browser to try to show the file inline
                }.ToString());
                //Console.WriteLine("size:" + response.FileStream.Length.ToString());
                return File(response.FileStream, response.ContentType);
            }
            else
            {
                return NotFound();
            }
        }
        [HttpGet("{fileName}")]
        public async Task<IActionResult> GetFile([FromRoute] string fileName)
        {
            return await GetS3File("", fileName);
        }
        [HttpGet("{folder}/{fileName}")]
        public async Task<IActionResult> GetFile([FromRoute] string folder, [FromRoute] string fileName)
        {
            return await GetS3File(folder, fileName);

        }
        [HttpPost]
        public async Task<IActionResult> AddFile()
        {
            IFormFile file = this.Request.Form.Files[0];
            S3Response response = await _service.UploadFileAsync(file);
            return Ok(response);
        }
        [HttpDelete("{fileName}")]
        public async Task<IActionResult> RemoveFile([FromRoute] string fileName)
        {
            S3Response response = await _service.RemoveFile(fileName);
            return Ok(response);
        }



    }
}