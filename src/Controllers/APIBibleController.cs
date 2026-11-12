using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/apib")]
public class APIBibleController(APIBibleService service) : ControllerBase
{
    private readonly APIBibleService _service = service;
    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages()
    {
        string response = await _service.GetLanguagesAsync();
        return Content(response, "application/json");
    }
    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("textbibles")]
    public async Task<IActionResult> GetAllTextBibles(
        [FromQuery] string? abbreviation,
        [FromQuery] string? name
    )
    {
        string response = await _service.GetTextBiblesAsync(null, abbreviation, name, null, true);
        return Content(response, "application/json");
    }
    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("textbibles/{language}")]
    public async Task<IActionResult> GetTextBibles(
        [FromRoute] string? language,
        [FromQuery] string? abbreviation,
        [FromQuery] string? name,
        [FromQuery] string? ids,
        [FromQuery(Name = "include-full-details")] bool? includeFullDetails
    )
    {
        string response = await _service.GetTextBiblesAsync(language, abbreviation, name, ids, includeFullDetails);
        return Content(response, "application/json");
    }

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("audiobibles/{language}")]
    public async Task<IActionResult> GetAudioBibles(
    [FromRoute] string language,
    [FromQuery] string? abbreviation,
    [FromQuery] string? name,
    [FromQuery] string? ids,
    [FromQuery] string? textBibleId,
    [FromQuery(Name = "include-full-details")] bool? includeFullDetails
)
    {
        string response = await _service.GetAudioBiblesAsync(language, abbreviation, name, ids, textBibleId, includeFullDetails);
        return Content(response, "application/json");
    }

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("audiotimings/{audioBibleId}")]
    public async Task<IActionResult> GetAudioTimings(
        [FromRoute] string audioBibleId,
        [FromQuery] string? books,
        [FromQuery] bool? sample
    )
    {
        string response = await _service.GetAudioTimingsAsync(audioBibleId, books, sample ?? false);
        return Content(response, "application/json");
    }

    // Passthroughs mirroring https://rest.api.bible/v1 paths.
    // bibles/{bibleId} also returns a single audio bible.

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}")]
    public async Task<IActionResult> GetBible([FromRoute] string bibleId) =>
        Json(await _service.GetBibleAsync(bibleId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/books")]
    public async Task<IActionResult> GetBooks(
        [FromRoute] string bibleId,
        [FromQuery(Name = "include-chapters")] bool? includeChapters,
        [FromQuery(Name = "include-chapters-and-sections")] bool? includeChaptersAndSections
    ) => Json(await _service.GetBooksAsync(bibleId, includeChapters, includeChaptersAndSections));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/books/{bookId}")]
    public async Task<IActionResult> GetBook(
        [FromRoute] string bibleId,
        [FromRoute] string bookId,
        [FromQuery(Name = "include-chapters")] bool? includeChapters
    ) => Json(await _service.GetBookAsync(bibleId, bookId, includeChapters));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/books/{bookId}/chapters")]
    public async Task<IActionResult> GetChapters([FromRoute] string bibleId, [FromRoute] string bookId) =>
        Json(await _service.GetChaptersAsync(bibleId, bookId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/books/{bookId}/sections")]
    public async Task<IActionResult> GetBookSections([FromRoute] string bibleId, [FromRoute] string bookId) =>
        Json(await _service.GetBookSectionsAsync(bibleId, bookId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/chapters/{chapterId}")]
    public async Task<IActionResult> GetChapter(
        [FromRoute] string bibleId,
        [FromRoute] string chapterId,
        [FromQuery] APIBibleContentOptions options
    ) => Json(await _service.GetChapterAsync(bibleId, chapterId, options));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/chapters/{chapterId}/verses")]
    public async Task<IActionResult> GetVerses([FromRoute] string bibleId, [FromRoute] string chapterId) =>
        Json(await _service.GetVersesAsync(bibleId, chapterId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/chapters/{chapterId}/sections")]
    public async Task<IActionResult> GetChapterSections([FromRoute] string bibleId, [FromRoute] string chapterId) =>
        Json(await _service.GetChapterSectionsAsync(bibleId, chapterId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/verses/{verseId}")]
    public async Task<IActionResult> GetVerse(
        [FromRoute] string bibleId,
        [FromRoute] string verseId,
        [FromQuery] APIBibleContentOptions options,
        [FromQuery(Name = "use-org-id")] bool? useOrgId
    ) => Json(await _service.GetVerseAsync(bibleId, verseId, options, useOrgId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/passages/{passageId}")]
    public async Task<IActionResult> GetPassage(
        [FromRoute] string bibleId,
        [FromRoute] string passageId,
        [FromQuery] APIBibleContentOptions options,
        [FromQuery(Name = "use-org-id")] bool? useOrgId
    ) => Json(await _service.GetPassageAsync(bibleId, passageId, options, useOrgId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/sections/{sectionId}")]
    public async Task<IActionResult> GetSection(
        [FromRoute] string bibleId,
        [FromRoute] string sectionId,
        [FromQuery] APIBibleContentOptions options
    ) => Json(await _service.GetSectionAsync(bibleId, sectionId, options));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("bibles/{bibleId}/search")]
    public async Task<IActionResult> Search(
        [FromRoute] string bibleId,
        [FromQuery] string? query,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] string? sort,
        [FromQuery] string? range,
        [FromQuery] string? fuzziness
    ) => Json(await _service.SearchAsync(bibleId, query, limit, offset, sort, range, fuzziness));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("audio-bibles/{audioBibleId}/books")]
    public async Task<IActionResult> GetAudioBooks(
        [FromRoute] string audioBibleId,
        [FromQuery(Name = "include-chapters")] bool? includeChapters,
        [FromQuery(Name = "include-chapters-and-sections")] bool? includeChaptersAndSections
    ) => Json(await _service.GetAudioBooksAsync(audioBibleId, includeChapters, includeChaptersAndSections));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("audio-bibles/{audioBibleId}/books/{bookId}")]
    public async Task<IActionResult> GetAudioBook(
        [FromRoute] string audioBibleId,
        [FromRoute] string bookId,
        [FromQuery(Name = "include-chapters")] bool? includeChapters
    ) => Json(await _service.GetAudioBookAsync(audioBibleId, bookId, includeChapters));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("audio-bibles/{audioBibleId}/books/{bookId}/chapters")]
    public async Task<IActionResult> GetAudioChapters([FromRoute] string audioBibleId, [FromRoute] string bookId) =>
        Json(await _service.GetAudioChaptersAsync(audioBibleId, bookId));

    [Authorize(Policy = "LocalOrAuthenticated")]
    [HttpGet("audio-bibles/{audioBibleId}/chapters/{chapterId}")]
    public async Task<IActionResult> GetAudioChapter([FromRoute] string audioBibleId, [FromRoute] string chapterId) =>
        Json(await _service.GetAudioChapterAsync(audioBibleId, chapterId));

    private ContentResult Json(string body) => Content(body, "application/json");
}
