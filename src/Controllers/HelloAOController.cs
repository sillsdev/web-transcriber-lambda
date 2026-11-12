using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/hao")]
public class HelloAOController(HelloAOService service) : ControllerBase
{
    private readonly HelloAOService _service = service;

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("{bibleId}/{book}/{chapter}")]
    public async Task<IActionResult> GetChapter(
        [FromRoute] string bibleId,
        [FromRoute] string book,
        [FromRoute] string chapter
    )
    {
        string response = await _service.GetChapterAsync(bibleId, book, chapter);
        return Content(response, "application/json");
    }
}
