using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

public class Orgkeytermreference : BaseModel, IArchive
{
    public int OrgkeytermId { get; set; }
    [HasOne(PublicName = "orgkeyterm")]
    public Orgkeyterm? Orgkeyterm { get; set; }

    public int BookId { get; set; }
    [HasOne(PublicName = "book")]
    public Book? Book { get; set; }

    [Attr(PublicName = "chapter")]
    public int Chapter { get; set; }
    [Attr(PublicName = "verse")]
    public int Verse { get; set; }
    public bool Archived { get; set; }

    [Attr(PublicName = "offline-id")]
    public string? OfflineId { get; set; }
}