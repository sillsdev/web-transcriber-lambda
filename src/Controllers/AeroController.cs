using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;
using SIL.Transcriber.Services.Contracts;

namespace SIL.Transcriber.Controllers;

/// <summary>
/// API to interact with Aero services
/// </summary>
/// <param name="service"></param>
/// <param name="loggerFactory"></param>
/// <param name="s3"></param>
[Route("api/[controller]")]
public class AeroController(AeroService service, ILoggerFactory loggerFactory, IS3Service s3) : Controller
{
    private readonly AeroService _service = service;
    private readonly IS3Service _s3 = s3;
    private ILogger Logger { get; set; } = loggerFactory.CreateLogger("AeroController");
    private ObjectResult HandleError(Exception ex, string context)
    {
        Logger.LogError(ex, "Error in {Context}: {Message}", context, ex.Message);
        return ex is HttpRequestException httpEx && httpEx.StatusCode.HasValue
            ? StatusCode((int)httpEx.StatusCode.Value, ex.Message)
            : StatusCode(500, ex.Message);
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

        try
        {
            string? taskId = await _service.NoiseRemoval(model.Data, model.FileName);
            return Ok(taskId);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(PostNR));
        }
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
        try
        {
            string? taskId = await _service.NoiseRemoval(request.FileUrl);
            return Ok(taskId);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(PostS3NR));
        }
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

        try
        {
            HttpContent? content = await _service.NoiseRemovalStatus(taskId);
            if (content != null)
            {
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
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckNR));
        }
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
        try
        {
            string? response = await _service.NoiseRemovalStatus(taskId, outputFile, "AI");
            if (response != null)
            {
                Models.S3Response url = _s3.SignedUrlForGet(outputFile, "AI", "audio/wav");
                return Ok(url);
            }
            return Ok(null);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckNRFile));
        }
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
        try
        {
            string? taskId = await _service.VoiceConversion(request.SourceUrl, request.TargetUrl);
            return Ok(taskId);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(PostS3VC));
        }
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

        try
        {
            HttpContent? content = await _service.VoiceConversionStatus(taskId);
            if (content != null)
            {
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
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckVC));
        }
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
        try
        {
            string? response = await _service.VoiceConversionStatus(taskId, outputFile, "AI");
            if (response != null)
            {
                Models.S3Response url = _s3.SignedUrlForGet(outputFile, "AI", "audio/wav");
                return Ok(url);
            }
            return Ok(null);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckVCFile));
        }
    }
    #endregion
    #region Transcription
    [AllowAnonymous]
    [HttpGet("transcription/languages")]
    public async Task<IActionResult> TranscriptionLanguages()
    {
        try
        {
            return Ok(await _service.TranscriptionLanguages());
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(TranscriptionLanguages));
        }
    }
    [AllowAnonymous]
    [HttpGet("transcription/asrlanguages/{iso}")]
    public async Task<IActionResult> TranscriptionAsrMethods([FromRoute] string iso)
    {
        try
        {
            return Ok(await _service.TranscriptionAsrMethods(iso));
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(TranscriptionAsrMethods));
        }
    }
    [AllowAnonymous]
    [HttpGet("transcription/asrsisters")]
    public async Task<IActionResult> TranscriptionAsrSisters([FromQuery] string userLanguage)
    {
        if (string.IsNullOrWhiteSpace(userLanguage))
            return BadRequest("userLanguage is required");
        try
        {
            return Ok(await _service.TranscriptionAsrSisters(userLanguage));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(TranscriptionAsrSisters));
        }
    }
    /// <summary>
    /// check to see if transcription task is complete
    /// </summary>
    /// <param name="taskId">taskId from voice conversion call</param>
    /// <returns>null if not done or a File</returns>
    [AllowAnonymous]
    [HttpGet("transcription/asrsisters/{taskId}")]
    public async Task<IActionResult> CheckSisters([FromRoute] string taskId)
    {
        try
        {
            return Ok(await _service.AsrSistersStatus(taskId));
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckSisters));
        }
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
        try
        {
            string[]? tasks = await _service.Transcription([request.FileUrl], request.Iso, request.Romanize);
            return Ok(tasks?.FirstOrDefault());
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(PostS3Transcription));
        }
    }
    /// <summary>
    /// check to see if transcription task is complete
    /// </summary>
    /// <param name="taskId">taskId from voice conversion call</param>
    /// <param name="phonetic">whether to check phonetic transcription</param>
    /// <returns>null if not done or a File</returns>
    [AllowAnonymous]
    [HttpGet("transcription/{taskId}")]
    public async Task<IActionResult> CheckTranscription([FromRoute] string taskId, [FromQuery] bool phonetic = false)
    {
        try
        {
            return Ok(await _service.TranscriptionStatus(taskId, phonetic));
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckTranscription));
        }
    }
    /// <summary>
    /// check to see if transcription task is complete
    /// </summary>
    /// <param name="taskId">taskId from voice conversion call</param>
    /// <returns>null if not done or a File</returns>
    [AllowAnonymous]
    [HttpGet("phonetic/{taskId}")]
    public async Task<IActionResult> CheckPhoneticTranscription([FromRoute] string taskId)
    {
        try
        {
            return Ok(await _service.TranscriptionStatus(taskId, true));
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckTranscription));
        }
    }
    #endregion
    #region Infilling
    /// <summary>
    /// send uploaded audio to send on to aero audio infilling
    /// </summary>
    /// <param name="model">load audio and parameters into body AudioInfillingRequestPhase1
    /// replacements format [ {"start": 8.04,"end": 9.00, "audio_format": "audio/mpeg", "audio_base64": "//..."}]</param>
    /// <returns>taskId</returns>
    [AllowAnonymous]
    [HttpPost("infilling")]
    public async Task<IActionResult> PostInfilling([FromBody] AudioInfillingFileUploadModelPhase1 model)
    {
        if (model == null || string.IsNullOrEmpty(model.Data))
        {
            return BadRequest("Invalid file data.");
        }

        // Require either ModifiedText or ReplacementAudioFiles/Replacements
        if (string.IsNullOrEmpty(model.Replacements))
        {
            return BadRequest("Replacements must be provided.");
        }
        /*
        // If ModifiedText is provided, InputText is required
        if (!string.IsNullOrEmpty(model.ModifiedText) && string.IsNullOrEmpty(model.InputText))
        {
            return BadRequest("InputText is required when ModifiedText is provided.");
        }
        */
        try
        {
            string? taskId = await _service.AudioInfilling(model.Data, model.FileName,  model.Replacements);
            return Ok(taskId);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(PostInfilling));
        }
    }
    /// <summary>
    /// send an s3 get url in the body to send on to aero audio infilling
    /// </summary>
    /// <param name="request">save audio to s3 and send link in AudioInfillingRequest body</param>
    /// <returns>taskId</returns>
    [AllowAnonymous]
    [HttpPost("infilling/fromfile")]
    public async Task<IActionResult> PostS3Infilling([FromBody] AudioInfillingRequest request)
    {
        if (string.IsNullOrEmpty(request.FileUrl))
        {
            return BadRequest("File URL is missing.");
        }

        // Require either ModifiedText or ReplacementAudioFiles/Replacements
        if (string.IsNullOrEmpty(request.ModifiedText) &&
            (request.ReplacementAudioFiles == null || request.ReplacementAudioFiles.Length == 0) &&
            string.IsNullOrEmpty(request.Replacements))
        {
            return BadRequest("Either ModifiedText or ReplacementAudioFiles/Replacements must be provided.");
        }

        // If ModifiedText is provided, InputText is required
        if (!string.IsNullOrEmpty(request.ModifiedText) && string.IsNullOrEmpty(request.InputText))
        {
            return BadRequest("InputText is required when ModifiedText is provided.");
        }

        Logger.LogInformation("Received S3 signed URL: {FileUrl}", request.FileUrl);
        try
        {
            string? taskId = await _service.AudioInfilling(request.FileUrl, request.ModifiedText,
                request.InputText, request.WordTimes, request.ReplacementAudioFiles, request.Replacements);
            return Ok(taskId);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(PostS3Infilling));
        }
    }
    /// <summary>
    /// check to see if audio infilling task is complete
    /// </summary>
    /// <param name="taskId">taskId from infilling call</param>
    /// <returns>null if not done or a File</returns>
    [AllowAnonymous]
    [HttpGet("infilling/{taskId}")]
    public async Task<IActionResult> CheckInfilling([FromRoute] string taskId)
    {
        try
        {
            HttpContent? content = await _service.AudioInfillingStatus(taskId);
            if (content != null)
            {
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
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckInfilling));
        }
    }
    /// <summary>
    /// check to see if audio infilling task is complete - save result to s3 file
    /// </summary>
    /// <param name="taskId">taskId from infilling call</param>
    /// <returns>null if not done or a link to an S3 File</returns>
    [AllowAnonymous]
    [HttpGet("infilling/s3/{taskId}")]
    public async Task<IActionResult> CheckInfillingFile([FromRoute] string taskId)
    {
        string outputFile = taskId + ".wav";
        try
        {
            string? response = await _service.AudioInfillingStatus(taskId, outputFile, "AI");
            if (response != null)
            {
                Models.S3Response url = _s3.SignedUrlForGet(outputFile, "AI", "audio/wav");
                return Ok(url);
            }
            return Ok(null);
        }
        catch (Exception ex)
        {
            return HandleError(ex, nameof(CheckInfillingFile));
        }
    }
    #endregion
}

