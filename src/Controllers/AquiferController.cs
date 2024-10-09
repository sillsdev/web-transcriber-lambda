using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
public class AquiferController : Controller
{
    private readonly AquiferService _aquiferService;
    private readonly ILogger Logger;
    public AquiferController(AquiferService service, ILoggerFactory loggerFactory)
    {
        _aquiferService = service;
        Logger = loggerFactory.CreateLogger<BiblebrainController>();
    }
    [HttpGet("languages")]
    public async Task<string> GetLanguages()
    {
        return await _aquiferService.GetLanguages();
    }
    [HttpGet("aquifer-search")]
    public async Task<string> Search([FromQuery] string bookCode, 
                                     [FromQuery] string languageCode, 
                                     [FromQuery] string limit,
                                     [FromQuery] string offset,
                                     [FromQuery] string? startChapter,
                                     [FromQuery] string? startVerse,
                                     [FromQuery] string? endChapter,
                                     [FromQuery] string? endVerse,
                                     [FromQuery] string? query)
    {
        return await _aquiferService.Search(bookCode, languageCode, limit, offset, startChapter, startVerse, endChapter, endVerse, query);
    }
    [HttpGet("content/{contentid}")]
    public async Task<string> GetContent([FromRoute] string contentid, [FromQuery] string contentTextType)
    {
        return await _aquiferService.GetContent(contentid, contentTextType);
    }

    [HttpPost]
    public async Task<string> Post([FromBody] AquiferPost content)
    {
        Console.WriteLine(content);
        //return content?.ToString()??"Null";
        return await _aquiferService.Post(content);
    }   

}
