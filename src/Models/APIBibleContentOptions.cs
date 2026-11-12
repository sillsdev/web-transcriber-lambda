using Microsoft.AspNetCore.Mvc;

namespace SIL.Transcriber.Models;

// Query options shared by the API.Bible chapter, verse, passage and section content endpoints.
// Only values that are supplied are forwarded, so API.Bible defaults apply otherwise.
public class APIBibleContentOptions
{
    [FromQuery(Name = "content-type")]
    public string? ContentType { get; set; } // html | json | text
    [FromQuery(Name = "include-notes")]
    public bool? IncludeNotes { get; set; }
    [FromQuery(Name = "include-titles")]
    public bool? IncludeTitles { get; set; }
    [FromQuery(Name = "include-chapter-numbers")]
    public bool? IncludeChapterNumbers { get; set; }
    [FromQuery(Name = "include-verse-numbers")]
    public bool? IncludeVerseNumbers { get; set; }
    [FromQuery(Name = "include-verse-spans")]
    public bool? IncludeVerseSpans { get; set; }
    [FromQuery(Name = "parallels")]
    public string? Parallels { get; set; } // comma-separated bible ids
}
