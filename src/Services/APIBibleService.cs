using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using System.Net.Http.Headers;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services;

public class APIBibleService
{
    private const string Domain = "https://rest.api.bible/v1/";
    private readonly string _apiKey = GetVarOrThrow("SIL_TR_API_BIBLE");

    private async Task<string> SendRequest(string path, string query)
    {
        using HttpClient client = new() { BaseAddress = new Uri(Domain) };
        Uri requestUri = new UriBuilder(new Uri(new Uri(Domain), path))
        {
            Query = query
        }.Uri;

        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        request.Headers.Add("api-key", _apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using HttpResponseMessage response = await client.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode
            ? body
            : throw new HttpRequestException(
                $"API.Bible request failed: {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                null,
                response.StatusCode
            );
    }
    public async Task<string> GetTextBiblesAsync(
        string? language,
        string? abbreviation,
        string? name,
        string? ids,
        bool? includeFullDetails
    //TODO: bool? canTTS
    )
    {
        List<string> parts = [];
        AddQueryPart(parts, "language", language);
        AddQueryPart(parts, "abbreviation", abbreviation);
        AddQueryPart(parts, "name", name);
        AddQueryPart(parts, "ids", ids);
        if (includeFullDetails ?? true)
            AddQueryPart(parts, "include-full-details", "true");
        string query = string.Join("&", parts);

        return await SendRequest("bibles", query);
    }
    public async Task<string> GetAudioBiblesAsync(
        string? language,
        string? abbreviation,
        string? name,
        string? ids,
        string? textBibleId,
        bool? includeFullDetails
    )
    {
        List<string> parts = [];
        AddQueryPart(parts, "language", language);
        AddQueryPart(parts, "abbreviation", abbreviation);
        AddQueryPart(parts, "name", name);
        AddQueryPart(parts, "ids", ids);
        AddQueryPart(parts, "bibleId", textBibleId);
        if (includeFullDetails ?? false)
            AddQueryPart(parts, "include-full-details", includeFullDetails.Value.ToString().ToLowerInvariant());
        string query = string.Join("&", parts);

        return await SendRequest("audio-bibles", query);
    }
    public async Task<string> GetLanguagesAsync()
    {
        string bibles = await GetTextBiblesAsync(null, null, null, null, null);
        JArray data = JObject.Parse(bibles)["data"] as JArray ?? [];
        IEnumerable<JToken> languages = data
            .Select(b => b["language"])
            .OfType<JObject>()
            .Where(l => !string.IsNullOrEmpty(l.Value<string>("id")))
            .DistinctBy(l => (l.Value<string>("id"), l.Value<string>("script")))
            .OrderBy(l => l.Value<string>("name"), StringComparer.OrdinalIgnoreCase);
        return new JObject { ["data"] = new JArray(languages) }.ToString(Formatting.None);
    }
    // Timecodes are only exposed on the single-chapter endpoint, so every chapter must be fetched.
    // books: optional comma-separated book ids to limit the scan.
    // sample: only check the first chapter of each book (quick "does this bible have timing" check).
    public async Task<string> GetAudioTimingsAsync(string audioBibleId, string? books, bool sample)
    {
        string bibleId = Uri.EscapeDataString(audioBibleId);
        JObject bookList = JObject.Parse(
            await SendRequest($"audio-bibles/{bibleId}/books", "include-chapters=true")
        );
        HashSet<string>? bookFilter = string.IsNullOrWhiteSpace(books)
            ? null
            : books.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<(string BookId, string ChapterId)> chapters = [];
        foreach (JToken book in bookList["data"] as JArray ?? [])
        {
            string bookId = book.Value<string>("id") ?? "";
            if (bookFilter != null && !bookFilter.Contains(bookId))
                continue;
            IEnumerable<string> ids = (book["chapters"] as JArray ?? [])
                .Where(c => c.Value<string>("number") != "intro")
                .Select(c => c.Value<string>("id") ?? "")
                .Where(id => id != "");
            if (sample)
                ids = ids.Take(1);
            chapters.AddRange(ids.Select(id => (bookId, id)));
        }

        using SemaphoreSlim throttle = new(5);
        JObject[] results = await Task.WhenAll(chapters.Select(async ch =>
        {
            await throttle.WaitAsync();
            try
            {
                JObject chapter = JObject.Parse(await SendRequest(
                    $"audio-bibles/{bibleId}/chapters/{Uri.EscapeDataString(ch.ChapterId)}", ""));
                int count = (chapter["data"]?["timecodes"] as JArray)?.Count ?? 0;
                return new JObject
                {
                    ["bookId"] = ch.BookId,
                    ["chapterId"] = ch.ChapterId,
                    ["timecodeCount"] = count,
                };
            }
            catch (HttpRequestException ex)
            {
                return new JObject
                {
                    ["bookId"] = ch.BookId,
                    ["chapterId"] = ch.ChapterId,
                    ["error"] = ex.Message,
                };
            }
            finally
            {
                throttle.Release();
            }
        }));

        int timed = results.Count(r => r.Value<int?>("timecodeCount") > 0);
        return new JObject
        {
            ["audioBibleId"] = audioBibleId,
            ["sample"] = sample,
            ["chaptersChecked"] = results.Length,
            ["chaptersWithTiming"] = timed,
            ["hasTiming"] = timed > 0,
            ["chapters"] = new JArray(results),
        }.ToString(Formatting.None);
    }
    public Task<string> GetBibleAsync(string bibleId) =>
        SendRequest($"bibles/{Esc(bibleId)}", "");

    public Task<string> GetBooksAsync(string bibleId, bool? includeChapters, bool? includeChaptersAndSections) =>
        SendRequest($"bibles/{Esc(bibleId)}/books", BuildQuery(
            ("include-chapters", Bool(includeChapters)),
            ("include-chapters-and-sections", Bool(includeChaptersAndSections))));

    public Task<string> GetBookAsync(string bibleId, string bookId, bool? includeChapters) =>
        SendRequest($"bibles/{Esc(bibleId)}/books/{Esc(bookId)}", BuildQuery(
            ("include-chapters", Bool(includeChapters))));

    public Task<string> GetChaptersAsync(string bibleId, string bookId) =>
        SendRequest($"bibles/{Esc(bibleId)}/books/{Esc(bookId)}/chapters", "");

    public Task<string> GetChapterAsync(string bibleId, string chapterId, APIBibleContentOptions options) =>
        SendRequest($"bibles/{Esc(bibleId)}/chapters/{Esc(chapterId)}", ContentQuery(options));

    public Task<string> GetVersesAsync(string bibleId, string chapterId) =>
        SendRequest($"bibles/{Esc(bibleId)}/chapters/{Esc(chapterId)}/verses", "");

    public Task<string> GetVerseAsync(string bibleId, string verseId, APIBibleContentOptions options, bool? useOrgId) =>
        SendRequest($"bibles/{Esc(bibleId)}/verses/{Esc(verseId)}", ContentQuery(options, ("use-org-id", Bool(useOrgId))));

    public Task<string> GetPassageAsync(string bibleId, string passageId, APIBibleContentOptions options, bool? useOrgId) =>
        SendRequest($"bibles/{Esc(bibleId)}/passages/{Esc(passageId)}", ContentQuery(options, ("use-org-id", Bool(useOrgId))));

    public Task<string> GetBookSectionsAsync(string bibleId, string bookId) =>
        SendRequest($"bibles/{Esc(bibleId)}/books/{Esc(bookId)}/sections", "");

    public Task<string> GetChapterSectionsAsync(string bibleId, string chapterId) =>
        SendRequest($"bibles/{Esc(bibleId)}/chapters/{Esc(chapterId)}/sections", "");

    public Task<string> GetSectionAsync(string bibleId, string sectionId, APIBibleContentOptions options) =>
        SendRequest($"bibles/{Esc(bibleId)}/sections/{Esc(sectionId)}", ContentQuery(options));

    public Task<string> SearchAsync(
        string bibleId,
        string? query,
        int? limit,
        int? offset,
        string? sort,
        string? range,
        string? fuzziness
    ) =>
        SendRequest($"bibles/{Esc(bibleId)}/search", BuildQuery(
            ("query", query),
            ("limit", limit?.ToString()),
            ("offset", offset?.ToString()),
            ("sort", sort),
            ("range", range),
            ("fuzziness", fuzziness)));

    public Task<string> GetAudioBooksAsync(string audioBibleId, bool? includeChapters, bool? includeChaptersAndSections) =>
        SendRequest($"audio-bibles/{Esc(audioBibleId)}/books", BuildQuery(
            ("include-chapters", Bool(includeChapters)),
            ("include-chapters-and-sections", Bool(includeChaptersAndSections))));

    public Task<string> GetAudioBookAsync(string audioBibleId, string bookId, bool? includeChapters) =>
        SendRequest($"audio-bibles/{Esc(audioBibleId)}/books/{Esc(bookId)}", BuildQuery(
            ("include-chapters", Bool(includeChapters))));

    public Task<string> GetAudioChaptersAsync(string audioBibleId, string bookId) =>
        SendRequest($"audio-bibles/{Esc(audioBibleId)}/books/{Esc(bookId)}/chapters", "");

    public Task<string> GetAudioChapterAsync(string audioBibleId, string chapterId) =>
        SendRequest($"audio-bibles/{Esc(audioBibleId)}/chapters/{Esc(chapterId)}", "");

    private static string Esc(string value) => Uri.EscapeDataString(value);

    private static string? Bool(bool? value) => value?.ToString().ToLowerInvariant();

    private static string ContentQuery(APIBibleContentOptions options, params (string Name, string? Value)[] extra) =>
        BuildQuery([
            ("content-type", options.ContentType),
            ("include-notes", Bool(options.IncludeNotes)),
            ("include-titles", Bool(options.IncludeTitles)),
            ("include-chapter-numbers", Bool(options.IncludeChapterNumbers)),
            ("include-verse-numbers", Bool(options.IncludeVerseNumbers)),
            ("include-verse-spans", Bool(options.IncludeVerseSpans)),
            ("parallels", options.Parallels),
            .. extra,
        ]);

    private static string BuildQuery(params (string Name, string? Value)[] values)
    {
        List<string> parts = [];
        foreach ((string name, string? value) in values)
            AddQueryPart(parts, name, value);
        return string.Join("&", parts);
    }

    private static void AddQueryPart(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
    }
}
