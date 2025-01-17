using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models;

public class Biblebrainsection : BaseModel
{
    [Attr(PublicName = "bible-id")]
    public string? BibleId { get; set; }
    [Attr(PublicName = "book-id")]
    public string? BookId { get; set; }
    [Attr(PublicName = "book-title")]
    public string? BookTitle { get; set; }

    [Attr(PublicName = "title")]
    public string? Title { get; set; }
    [Attr(PublicName = "start-chapter")]
    public int? StartChapter { get; set; }

    [Attr(PublicName = "start-verse")]
    public int? StartVerse { get; set; }

    [Attr(PublicName = "end-chapter")]
    public int? EndChapter { get; set; }

    [Attr(PublicName = "end-verse")]
    public int? EndVerse { get; set; }
}
