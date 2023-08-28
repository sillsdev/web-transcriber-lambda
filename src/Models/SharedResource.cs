using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models;
[Table(Tables.SharedResources)]

public partial class Sharedresource : BaseModel, IArchive
{
    [Attr(PublicName = "passage-id")]
    public int? PassageId { get; set; }
    [HasOne(PublicName = "passage")]
    public Passage? Passage { get; set; }

    [Attr(PublicName = "cluster-id")]
    public int? ClusterId { get; set; }
    [HasOne(PublicName = "cluster")]
    public Organization? Cluster { get; set; }

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
    [Attr(PublicName = "title-mediafile-id")]
    [ForeignKey(nameof(TitleMediafile))]
    public int? TitleMediafileId { get; set; }

    [HasOne(PublicName = "title-mediafile")]
    public Mediafile? TitleMediafile { get; set; }

    [Attr(PublicName = "note")]
    public bool Note { get; set; }

    [Attr(PublicName = "link-url")]
    public string? LinkUrl { get; set; }

    public bool Archived { get; set; }
}
