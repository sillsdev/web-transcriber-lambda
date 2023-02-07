using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace SIL.Transcriber.Models;

public class Orgkeytermtarget : BaseModel, IArchive
{
    public int OrganizationId { get; set; }
    [HasOne(PublicName = "organization")]
    public Organization? Organization { get; set; }
    [Attr(PublicName = "term")] 
    public string Term { get; set; } = "";
        
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