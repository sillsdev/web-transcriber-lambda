using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

public class Sharedresourcereference : BaseModel, IArchive
{
    public int SharedResourceId { get; set; }
    [HasOne(PublicName = "shared-resource")]
    public Sharedresource? SharedResource { get; set; }

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