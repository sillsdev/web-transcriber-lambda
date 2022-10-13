using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public partial class Resource : BaseModel
    {
        public int ProjectId { get; set; }
        [Attr(PublicName = "project-name")]
        public string? ProjectName { get; set; }

        public int OrganizationId { get; set; }
        [Attr(PublicName = "organization")]
        public string? Organization { get; set; }

        [Attr(PublicName = "language")]
        public string? Language { get; set; }

        public int PlanId { get; set; }
        [Attr(PublicName = "plan")]
        public string? Planname { get; set; }
        [Attr(PublicName = "plantype")]
        public string? Plantype { get; set; }

        public int SectionId { get; set; }
        [Attr(PublicName = "section")]
        public string? SectionName { get; set; }
        [Attr(PublicName = "section-sequencenum")]
        public int? SectionSequencenum { get; set; }


        [Attr(PublicName = "passage-id")]
        public int? Passageid { get; set; }
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
    }
}
