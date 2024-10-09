using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
public class BiblebrainController : Controller
{
    private readonly BibleBrainService _bibleBrainService;
    private readonly ILogger Logger;
    public BiblebrainController(BibleBrainService service, ILoggerFactory loggerFactory)
    {
        _bibleBrainService = service;
        Logger = loggerFactory.CreateLogger<BiblebrainController>();
    }
    [HttpGet("{bibleid}/{size}/{timing}/copyright")]
    public async Task<string> GetCopyright([FromRoute] string bibleid, 
                                            [FromRoute] string Size, 
                                            [FromRoute] bool timing)
    {
        return await _bibleBrainService.GetCopyright(bibleid, Size, timing);
    }
    /*
    [HttpGet("languages")]
    public async Task<string> GetLanguages([FromQuery] string country,
        [FromQuery] string languageCode,
        [FromQuery] string languageName,
        [FromQuery] string includeTranslations,
        [FromQuery] string l10n,
        [FromQuery] string page,
        [FromQuery] string limit)
    {
        return await _bibleBrainService.GetLanguages(country, languageCode, languageName, includeTranslations, l10n, page, limit);

    }
    [HttpGet("bibles/{lang}")]
    public async Task<string> GetBibles([FromRoute] string lang, [FromQuery] string? media, [FromQuery] string? page, [FromQuery] string? limit)
    {
        short.TryParse(page, out short nPage);
        short.TryParse(limit, out short nLimit);
        return await _bibleBrainService.GetBibles(lang, media, false, nPage, nLimit);
    }
    [HttpGet("bibles/{lang}/timing")]
    public async Task<string> GetBiblesWithTiming([FromRoute] string lang, [FromQuery] string? media, [FromQuery] string? page, [FromQuery] string? limit)
    {
        short.TryParse(page, out short nPage);
        short.TryParse(limit, out short nLimit);
        return await _bibleBrainService.GetBibles(lang, media, true, nPage, nLimit);
    }
    */

    [HttpPost]
    public async Task<string> Post([FromBody] BiblebrainPost content)
    {
        Console.WriteLine(content);
        //return content?.ToString()??"Null";
        return await _bibleBrainService.Post(content);
    }
}
