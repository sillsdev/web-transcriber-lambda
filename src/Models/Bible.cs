using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;

[Table(Tables.Bibles)]
public class Bible : BaseModel, IArchive
{
    [Attr(PublicName = "bible-id")]
    public string BibleId { get; set; } = "";
    [Attr(PublicName = "abbr")]
    public string? Abbr => BibleId.Length > 3 ? BibleId [3..] : BibleId;
    [Attr(PublicName = "iso")]
    public string Iso { get; set; } = "";
    [Attr(PublicName = "bible-name")]
    public string BibleName { get; set; } = "";
    [Attr(PublicName = "description")]
    public string? Description { get; set; }
    [Attr(PublicName = "publishing-data")]
    [Column(TypeName = "jsonb")]
    public string? PublishingData { get; set; } //json

    [ForeignKey("BibleMediafile")]
    [Attr(PublicName = "bible-mediafile-id")]
    public int? BibleMediafileId { get; set; }

    [HasOne(PublicName = "bible-mediafile")]
    public virtual Mediafile? BibleMediafile { get; set; }

    [ForeignKey("IsoMediafile")]
    [Attr(PublicName = "iso-mediafile-id")]
    public int? IsoMediafileId { get; set; }
    [HasOne(PublicName = "iso-mediafile")]
    public virtual Mediafile? IsoMediafile { get; set; }
    [Attr(PublicName = "any-published")]
    public bool AnyPublished { get; set; }

    //[ForeignKey("OwnerOrganization")]
    //[Attr(PublicName = "owner-organization-id")]
    //public int? OwnerOrganizationId { get; set; }

    //[HasOne(PublicName = "owner-organization")]
    //public virtual Organization? OwnerOrganization { get; set; }
    public bool Archived { get; set; }
}

