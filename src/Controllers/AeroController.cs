using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
public class AeroController : Controller
{
    private readonly AeroService _service;
    private ILogger Logger { get; set; }
    public AeroController(AeroService service, ILoggerFactory loggerFactory)
    {
        _service = service;
        Logger = loggerFactory.CreateLogger("AeroController");
    }
    private static async Task<MemoryStream> ConvertToMemoryStream(IFormFile formFile)
    {
        MemoryStream memoryStream = new ();
        await formFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0; // Reset the position to the beginning of the stream
        return memoryStream;
    }

    [AllowAnonymous]
    [HttpPost("noiseremoval")]
    public async Task<IActionResult> PostNR(IFormFile file)
    {
        Logger.LogCritical("File name: {FileName}", file.FileName);
        Logger.LogCritical("File length: {FileLength}", file.Length);
        //IFormFile file = Request.Form.Files[0];
        string? taskId = await _service.NoiseRemoval(file);
        return Ok(taskId);
    }
    [AllowAnonymous]
    [HttpPost("noiseremoval/fromfile")]
    public async Task<IActionResult> PostS3NR([FromBody] FileUrlRequest request)
    {
        if (string.IsNullOrEmpty(request.FileUrl))
        {
            return BadRequest("File URL is missing.");
        }
        Logger.LogInformation("Received S3 signed URL: {FileUrl}", request.FileUrl);

        string? taskId = await _service.NoiseRemoval(request.FileUrl);
        return Ok(taskId);
    }

    [AllowAnonymous]
    [HttpGet("noiseremoval/{taskId}")]
    public async Task<IActionResult> CheckNR([FromRoute] string taskId)
    {

        HttpContent? content =  await _service.NoiseRemovalStatus(taskId);
        if (content != null)
        {
            // Read the response content as a stream
            Stream stream = await content.ReadAsStreamAsync();

            // Return the stream as a file in the API response
            return File(stream, content.Headers.ContentType?.MediaType ?? "audio/mpeg", content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "downloaded.mp3");
        }
        return Ok(null);

    }
}
public class FileUrlRequest
{
    public string FileUrl { get; set; } = "";
}