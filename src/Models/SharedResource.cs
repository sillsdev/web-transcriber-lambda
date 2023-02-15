using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;
[Table("sharedresources")]

public partial class Sharedresource : BaseModel, IArchive
{
    [Attr(PublicName = "mediafile-id")]
    public int? MediafileId { get; set; }
    [HasOne(PublicName = "mediafile")]
    public Mediafile? Mediafile { get; set; }

    [Attr(PublicName = "title")]
    public string? Title { get; set; }
    [Attr(PublicName = "description")]
    public string? Description { get; set; }
    [Attr(PublicName = "languagebcp47")]
    public string? Languagebcp47 { get; set; }

    [Attr(PublicName = "terms-of-use")]
    public string? TermsOfUse { get; set; }

    [Attr(PublicName = "keywords")]
    public string? Keywords { get; set; }

    public int? ArtifactCategoryId { get; set; }

    [HasOne(PublicName = "artifact-category")]
    public Artifactcategory? ArtifactCategory { get; set; }

    [Attr(PublicName = "offline-id")]
    public string? OfflineId { get; set; }
    [Attr(PublicName = "offline-mediafile-id")]
    public string? OfflineMediafileId { get; set; }
    public bool Archived { get; set; }
}
