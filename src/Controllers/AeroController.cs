using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
public class AeroController : Controller
{
    private readonly AeroService _service;
    private readonly IS3Service _s3;
    private ILogger Logger { get; set; }
    public AeroController(AeroService service, ILoggerFactory loggerFactory, IS3Service s3)
    {
        _service = service;
        Logger = loggerFactory.CreateLogger("AeroController");
        _s3 = s3;
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
    public async Task<IActionResult> PostNR([FromBody] FileUploadModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.Data))
        {
            return BadRequest("Invalid file data.");
        }

        string? taskId = await _service.NoiseRemoval(model.Data, model.FileName);
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

        string? taskId = await _service.NoiseRemovalS3(request.FileUrl);
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
            byte[] fileBytes = await content.ReadAsByteArrayAsync();
            string base64String = Convert.ToBase64String(fileBytes);

            return Ok(new
            {
                FileName = content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "downloaded.wav",
                ContentType = content.Headers.ContentType?.MediaType ?? "audio/wav",
                Data = base64String,
                IsBase64Encoded = true,
            });
        }
        return Ok(null);

    }
    [AllowAnonymous]
    [HttpGet("noiseremoval/s3/{taskId}")]
    public async Task<IActionResult> CheckNRFile([FromRoute] string taskId)
    {
        string outputFile = taskId + ".wav";
        string? response = await _service.NoiseRemovalStatus(taskId, outputFile, "AI");
        if (response != null)
        {
            Models.S3Response url = _s3.SignedUrlForGet(outputFile, "AI", "audio/wav");
            return Ok(url);
        }
        return Ok(null);
    }
}
public class FileUrlRequest
{
    public string FileUrl { get; set; } = "";
}
public class FileUploadModel
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Data { get; set; } = "";
}