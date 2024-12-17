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
    /// <summary>
    /// API to interact with Aero services
    /// </summary>
    /// <param name="service"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="s3"></param>
    public AeroController(AeroService service, ILoggerFactory loggerFactory, IS3Service s3)
    {
        _service = service;
        Logger = loggerFactory.CreateLogger("AeroController");
        _s3 = s3;
    }
    #region NoiseRemoval
    /// <summary>
    /// send uploaded audio to send on to aero noise removal
    /// </summary>
    /// <param name="model">load audio into body FileUploadModel</param>
    /// <returns>taskId</returns>
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
    /// <summary>
    /// send an s3 get url in the body to send on to aero noise removal
    /// </summary>
    /// <param name="request">save audio to s3 and send link in FileUrlRequest body</param>
    /// <returns>taskId</returns>
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
    /// <summary>
    /// check to see if noise removal task is complete
    /// </summary>
    /// <param name="taskId">taskId from noiseremoval call</param>
    /// <returns>null if not done or a File</returns>
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
    /// <summary>
    /// check to see if noise removal task is complete - save result to s3 file
    /// </summary>
    /// <param name="taskId">taskId from noiseremoval call</param>
    /// <returns>null if not done or a link to an S3 File</returns>
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
    #endregion
    #region VoiceConversion
    /// <summary>
    /// send an s3 get url in the body to send on to aero voice conversion
    /// </summary>
    /// <param name="request">save audio to s3 and send link in FileUrlRequest body</param>
    /// <returns>taskId</returns>
    [AllowAnonymous]
    [HttpPost("voiceconversion/fromfile")]
    public async Task<IActionResult> PostS3VC([FromBody] SourceTargetRequest request)
    {
        if (string.IsNullOrEmpty(request.SourceUrl) || string.IsNullOrEmpty(request.TargetUrl))
        {
            return BadRequest("File URL is missing.");
        }
        Logger.LogInformation("Received S3 signed URL: {S} {T}", request.SourceUrl, request.TargetUrl);

        string? taskId = await _service.VoiceConversion(request.SourceUrl, request.TargetUrl);
        return Ok(taskId);
    }
    /// <summary>
    /// check to see if voice conversion task is complete
    /// </summary>
    /// <param name="taskId">taskId from voice conversion call</param>
    /// <returns>null if not done or a File</returns>
    [AllowAnonymous]
    [HttpGet("voiceconversion/{taskId}")]
    public async Task<IActionResult> CheckVC([FromRoute] string taskId)
    {

        HttpContent? content =  await _service.VoiceConversionStatus(taskId);
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
    /// <summary>
    /// check to see if voice conversion task is complete - save result to s3 file
    /// </summary>
    /// <param name="taskId">taskId from voice conversion call</param>
    /// <returns>null if not done or a link to an S3 File</returns>
    [AllowAnonymous]
    [HttpGet("voiceconversion/s3/{taskId}")]
    public async Task<IActionResult> CheckVCFile([FromRoute] string taskId)
    {
        string outputFile = taskId + ".wav";
        string? response = await _service.VoiceConversionStatus(taskId, outputFile, "AI");
        if (response != null)
        {
            Models.S3Response url = _s3.SignedUrlForGet(outputFile, "AI", "audio/wav");
            return Ok(url);
        }
        return Ok(null);
    }
    #endregion
    #region Transcription
    /// <summary>
    /// </summary>
    /// <returns>taskId</returns>
    [AllowAnonymous]
    [HttpGet("transcription/languages")]
    public async Task<IActionResult> TranscriptionLanguages()
    {
        return Ok(await _service.TranscriptionLanguages());
    }
    /// <summary>
    /// send an s3 get url in the body to send on to aero transcription
    /// </summary>
    /// <param name="request">save audio to s3 and send link in FileUrlRequest body</param>
    /// <returns>taskId</returns>
    [AllowAnonymous]
    [HttpPost("transcription/fromfile")]
    public async Task<IActionResult> PostS3Transcription([FromBody] TranscriptionRequest request)
    {
        if (string.IsNullOrEmpty(request.FileUrl) || string.IsNullOrEmpty(request.Iso))
        {
            return BadRequest("File URL or Iso is missing.");
        }
        Logger.LogInformation("Received URL: {S} {L}", request.FileUrl, request.Iso);

        string? taskId = await _service.Transcription(request.FileUrl, request.Iso, request.Romanize);
        return Ok(taskId);
    }
    /// <summary>
    /// check to see if voice conversion task is complete
    /// </summary>
    /// <param name="taskId">taskId from voice conversion call</param>
    /// <returns>null if not done or a File</returns>
    [AllowAnonymous]
    [HttpGet("transcription/{taskId}")]
    public async Task<IActionResult> CheckTranscription([FromRoute] string taskId)
    {
        return Ok(await _service.TranscriptionStatus(taskId));
    }
    #endregion
}

