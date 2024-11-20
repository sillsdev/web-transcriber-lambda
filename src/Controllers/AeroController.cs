using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Diagnostics.Eventing.Reader;
using System.IO;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
public class AeroController : Controller
{
    private readonly AeroService _service;
    public AeroController(AeroService service)
    {
        _service = service;
    }
    private async Task<MemoryStream> ConvertToMemoryStream(IFormFile formFile)
    {
        MemoryStream memoryStream = new MemoryStream();
        await formFile.CopyToAsync(memoryStream);
        memoryStream.Position = 0; // Reset the position to the beginning of the stream
        return memoryStream;
    }

    [AllowAnonymous]
    [HttpPost("noiseremoval")]
    public async Task<IActionResult> PostNR()
    {
        IFormFile file = Request.Form.Files[0];
        string? taskId = await _service.NoiseRemoval(file);
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
            return File(stream, content.Headers.ContentType?.MediaType??"audio/mpeg", content.Headers.ContentDisposition?.FileName?.Trim('"')??"downloaded.mp3");
        }
        return Ok(null);
       
    }
}