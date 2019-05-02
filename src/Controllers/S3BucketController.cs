using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class S3BucketController : ControllerBase
    {
        private const string myProject = "Project0";
        private readonly IS3Service _service;
        public S3BucketController(IS3Service service)
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
            S3Response response = await _service.ListObjectsAsync(myProject);
            return Ok(response);
        }

        [HttpGet("{fileName}")]
        public async Task<IActionResult> GetFile([FromRoute] string fileName)
        {
            //somehow I'll know what project this is for...perhaps just the taskid will be passed in?

            S3Response response = await _service.ReadObjectDataAsync(fileName, myProject);

            Response.Headers.Add("Content-Disposition", new ContentDisposition
            {
                FileName = fileName,
                Inline = true // false = prompt the user for downloading; true = browser to try to show the file inline
            }.ToString());

            return File(response.FileStream, response.ContentType); 
        }
        [HttpPost]
        public async Task<IActionResult> AddFile()
        {
            IFormFile file = this.Request.Form.Files[0];
            S3Response response = await _service.UploadFileAsync(file, myProject);
            return Ok(response);
        }
        [HttpDelete("{fileName}")]
        public async Task<IActionResult> RemoveFile([FromRoute] string fileName)
        {
            S3Response response = await _service.RemoveFile(fileName, myProject);
            return Ok(response);
        }



    }
}