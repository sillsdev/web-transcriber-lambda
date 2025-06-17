using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Net;
using System.Net.Mime;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

//Transcriber doesn't currently use this controller

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    public class S3FilesController(IS3Service service) : ControllerBase
    {
        private readonly IS3Service _service = service;

        [HttpGet]
        public async Task<IActionResult> ListFiles()
        {
            S3Response s3response = await _service.ListObjectsAsync();

            return s3response.Status == HttpStatusCode.OK ? Ok(s3response.Message) : (IActionResult)Ok(s3response);
        }

        private async Task<IActionResult> GetS3File(
            string folder,
            string fileName,
            string fileNameOut = ""
        )
        {
            S3Response response = await _service.ReadObjectDataAsync(fileName, folder);

            if (response.Status == HttpStatusCode.OK)
            {
                Response.Headers.Append(
                    "Content-Disposition",
                    new ContentDisposition
                    {
                        FileName = fileNameOut.Length > 0 ? fileNameOut : fileName,
                        Inline = true // false = prompt the user for downloading; true = browser to try to show the file inline
                    }.ToString()
                );
                //Console.WriteLine("size:" + response.FileStream.Length.ToString());
                return response.FileStream != null
                    ? File(response.FileStream, response.ContentType)
                    : NotFound();
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
        public async Task<IActionResult> GetFile(
            [FromRoute] string folder,
            [FromRoute] string fileName
        )
        {
            return await GetS3File(folder, fileName);
        }
        [HttpDelete("AI/{fileName}")]
        public async Task<IActionResult> RemoveAIFile(
            [FromRoute] string folder,
            [FromRoute] string fileName)
        {
            string Bucket = GetVarOrThrow("SIL_TR_AERO_BUCKET");
            S3Response response = await _service.RemoveFile(fileName, folder, Bucket);
            return Ok(response);
        }

        [HttpDelete("{folder}/{fileName}")]
        public async Task<IActionResult> RemoveFile(
            [FromRoute] string folder,
            [FromRoute] string fileName)
        {
            S3Response response = await _service.RemoveFile(fileName, folder);
            return Ok(response);
        }
        [HttpGet("put/AI/{fileName}/{contentType}")]
        public IActionResult PutURL(
            [FromRoute] string fileName,
            [FromRoute] string contentType)
        {
            contentType = "audio/" + contentType;
            return Ok(_service.SignedUrlForPut(fileName, "input_files", contentType, GetVarOrThrow("SIL_TR_AERO_BUCKET"), GetVarOrThrow("SIL_TR_AWS_KEY"), GetVarOrThrow("SIL_TR_AWS_SECRET")).Message);

        }
        //don't think this is used
        [HttpGet("put/{folder}/{fileName}/{contentType}")]
        public IActionResult PutURL(
            [FromRoute] string folder,
            [FromRoute] string fileName,
            [FromRoute] string contentType)
        {
            contentType = "audio/" + contentType;
            return Ok(_service.SignedUrlForPut(fileName, folder, contentType).Message);

        }
        [HttpGet("get/{folder}/{fileName}/{contentType}")]
        public IActionResult GetURL(
                [FromRoute] string folder,
                [FromRoute] string fileName,
                [FromRoute] string contentType)
        {
            contentType = "audio/" + contentType;
            return Ok(_service.SignedUrlForGet(fileName, folder, contentType).Message);
        }
    }
}
