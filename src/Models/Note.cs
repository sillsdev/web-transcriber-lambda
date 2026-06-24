using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models;

public partial class Note: BaseModel
{
    [Attr(PublicName = "project-id")]
    public int ProjectId { get; set; }
    [Attr(PublicName = "project-name")]
    public string? ProjectName { get; set; }

    [Attr(PublicName = "organization-id")]
    public int OrganizationId { get; set; }
    [Attr(PublicName = "organization")]
    public string? Organization { get; set; }

    [Attr(PublicName = "language")]
    public string? Language { get; set; }

    [Attr(PublicName = "plan-id")]
    public int PlanId { get; set; }
    [Attr(PublicName = "plan")]
    public string? Planname { get; set; }
    [Attr(PublicName = "plantype")]
    public string? Plantype { get; set; }

    [Attr(PublicName = "section-id")]
    public int SectionId { get; set; }
    [Attr(PublicName = "section")]
    public string? SectionName { get; set; }
    [Attr(PublicName = "section-sequencenum")]
    public int? SectionSequencenum { get; set; }

    [Attr(PublicName = "mediafile-id")]
    public int? MediafileId { get; set; }
    [Attr(PublicName = "passage-id")]
    public int? PassageId { get; set; }
    [Attr(PublicName = "passage-sequencenum")]
    public int PassageSequencenum { get; set; }

    [Attr(PublicName = "book")]
    public string? Book { get; set; }
    [Attr(PublicName = "reference")]
    public string? Reference { get; set; }
    [Attr(PublicName = "passage-desc")]
    public string? PassageDesc { get; set; }
    [Attr(PublicName = "version-number")]
    public int? VersionNumber { get; set; }

    [Attr(PublicName = "audio-url")]
    public string? AudioUrl { get; set; }
    [Attr(PublicName = "duration")]
    public int? Duration { get; set; }
    [Attr(PublicName = "content-type")]
    public string? ContentType { get; set; }
    [Attr(PublicName = "transcription")]
    public string? Transcription { get; set; }

    [Attr(PublicName = "original-file")]
    public string? OriginalFile { get; set; }
    [Attr(PublicName = "s3file")]
    public string? S3File { get; set; }
    [Attr(PublicName = "filesize")]
    public long Filesize { get; set; }

    [Attr(PublicName = "languagebcp47")]
    public string? Languagebcp47 { get; set; }

    [Attr(PublicName = "category-name")]
    public string? CategoryName { get; set; }
    [Attr(PublicName = "type-name")]
    public string? TypeName { get; set; }
    [Attr(PublicName = "latest")]
    public bool Latest { get; set; }

    [Attr(PublicName = "cluster-id")]
    public int? ClusterId { get; set; }
    [HasOne(PublicName = "cluster")]
    public Organization? Cluster { get; set; }

    [Attr(PublicName = "title")]
    public string? Title { get; set; }

    [Attr(PublicName = "description")]
    public string? Description { get; set; }

    [Attr(PublicName = "terms-of-use")]
    public string? TermsOfUse { get; set; }
    [Attr(PublicName = "keywords")]
    public string? Keywords { get; set; }

    [Attr(PublicName = "resource-id")]
    public int? ResourceId { get; set; }
}
