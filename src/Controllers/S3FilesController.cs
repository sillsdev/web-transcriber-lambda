using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Net;
using System.Net.Mime;

//Transcriber doesn't currently use this controller

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    public class S3FilesController : ControllerBase
    {
        private readonly IS3Service _service;

        public S3FilesController(IS3Service service)
        {
            _service = service;
        }

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
                Response.Headers.Add(
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


        [HttpDelete("{fileName}")]
        public async Task<IActionResult> RemoveFile([FromRoute] string fileName)
        {
            S3Response response = await _service.RemoveFile(fileName);
            return Ok(response);
        }
    }
}
