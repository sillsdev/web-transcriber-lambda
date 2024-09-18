using Microsoft.AspNetCore.Authorization;
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
    [AllowAnonymous]
    [HttpGet("languages")]
    public async Task<string> GetLanguages()
    {
        return await _bibleBrainService.GetLanguages();

    }
    [AllowAnonymous]
    [HttpGet("bibles/{lang}")]
    public async Task<string> GetBibles([FromRoute] string lang)
    {
        return await _bibleBrainService.GetBibles(lang, false);
    }
    [AllowAnonymous]
    [HttpGet("bibles/{lang}/timing")]
    public async Task<string> GetBiblesWithTiming([FromRoute] string lang)
    {
        return await _bibleBrainService.GetBibles(lang, true);
    }


}
