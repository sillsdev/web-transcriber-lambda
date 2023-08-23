using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models;

public class Orgkeytermtarget : BaseModel, IArchive
{
    [Attr(PublicName = "organization-id")]
    public int OrganizationId { get; set; }
    [HasOne(PublicName = "organization")]
    public Organization? Organization { get; set; }
    [Attr(PublicName = "term")] 
    public string? Term { get; set; }  //only the org specific words will be here

    [Attr(PublicName = "term-index")]
    public int? TermIndex { get; set; }  //the standard words will use this

    [Attr(PublicName = "target")]
    public string? Target { get; set; }

    [Attr(PublicName = "mediafile-id")]
    public int? MediafileId { get; set; }
    [HasOne(PublicName = "mediafile")]
    public Mediafile? Mediafile { get; set; }
    public bool Archived { get; set; }
    [Attr(PublicName = "offline-id")]
    public string? OfflineId { get; set; }
    [Attr(PublicName = "offline-mediafile-id")]
    public string? OfflineMediafileId { get; set; }
}