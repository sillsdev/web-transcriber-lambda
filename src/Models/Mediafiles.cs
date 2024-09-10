using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;


namespace SIL.Transcriber.Models
{
    [Table(Tables.Mediafiles)]
    public partial class Mediafile : BaseModel, IArchive
    {
        [Attr(PublicName = "passage-id")]
        public int? PassageId { get; set; }
        [HasOne(PublicName = "passage")]
        public virtual Passage? Passage { get; set; }

        [Attr(PublicName = "plan-id")]
        public int PlanId { get; set; }
        [HasOne(PublicName = "plan")]
        public virtual Plan? Plan { get; set; }

        [Attr(PublicName = "artifact-type-id")]
        public int? ArtifactTypeId { get; set; }
        [HasOne(PublicName = "artifact-type")]
        public Artifacttype? ArtifactType { get; set; }
        [Attr(PublicName = "artifact-category-id")]
        public int? ArtifactCategoryId { get; set; }
        [HasOne(PublicName = "artifact-category")]
        public Artifactcategory? ArtifactCategory { get; set; }

        [Attr(PublicName = "version-number")]
        public int? VersionNumber { get; set; }
        [Attr(PublicName = "eaf-url")]
        public string? EafUrl { get; set; }
        [Attr(PublicName = "audio-url")]
        public string? AudioUrl { get; set; }
        [Attr(PublicName = "duration")]
        public int? Duration { get; set; }
        [Attr(PublicName = "content-type")]
        public string? ContentType { get; set; }
        [Attr(PublicName = "audio-quality")]
        public string? AudioQuality { get; set; }
        [Attr(PublicName = "text-quality")]
        public string? TextQuality { get; set; }
        [Attr(PublicName = "transcription")]
        public string? Transcription { get; set; }

        [Attr(PublicName = "original-file")]
        public string? OriginalFile { get; set; }
        [Attr(PublicName = "s3file")]
        public string? S3File { get; set; }
        [Attr(PublicName = "published-as")]
        public string? PublishedAs { get; set; }

        [Attr(PublicName = "publish-to")]
        [Column(TypeName = "jsonb")]
        public string? PublishTo { get; set; } = "{}";
        [Attr(PublicName = "filesize")]
        public long Filesize { get; set; }
        [Attr(PublicName = "position")]
        public double Position { get; set; }
        [Attr(PublicName = "topic")]
        public string? Topic { get; set; }
        [Attr(PublicName = "transcriptionstate")]
        public string? Transcriptionstate { get; set; }
        public bool Archived { get; set; }

        [Attr(PublicName = "segments")]
        [Column(TypeName = "jsonb")]
        public string? Segments { get; set; }

        //public int? RecordedbyUserId { get; set; }
        [HasOne(PublicName = "recordedby-user")]
        virtual public User? RecordedbyUser { get; set; }

        [Attr(PublicName = "languagebcp47")]
        public string? Languagebcp47 { get; set; }

        [Attr(PublicName = "performed-by")]
        public string? PerformedBy { get; set; }
        [Attr(PublicName = "ready-to-share")]
        public bool ReadyToShare { get; set; }
        [Attr(PublicName = "resource-passage-id")]
        public int? ResourcePassageId { get; set; }
        [HasOne(PublicName = "resource-passage")]
        public Passage? ResourcePassage { get; set; }
        [Attr(PublicName = "link")]
        public bool? Link { get; set; }

        [Attr(PublicName = "offline-id")]
        public string? OfflineId { get; set; }
        [Attr(PublicName = "source-media-offline-id")]
        public string? SourceMediaOfflineId { get; set; }

        [Attr(PublicName = "source-media-id")]
        public int? SourceMediaId { get; set; }
        [HasOne(PublicName = "source-media")]
        public Mediafile? SourceMedia { get; set; }

        [Attr(PublicName = "source-segments")]
        [Column(TypeName = "jsonb")]
        public string? SourceSegments { get; set; }

        public bool ReadyToSync {
            get { return Transcriptionstate == "approved" && !Archived; }
        }
        public bool IsVernacular {
            get { return ArtifactTypeId is null; }
        }

    }
    [Table(Tables.Mediafiles)]
    public partial class SourceMediafile : Mediafile
    {

    }

}
