using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Mediafile : BaseModel, IArchive
    {
        [Attr("passage-id")]
        public int? PassageId { get; set; }
        [HasOne("passage", Link.None)]
        public Passage Passage { get; set; }

        [Attr("plan-id")]
        public int PlanId { get; set; }
        [HasOne("plan", Link.None)]
        public Plan Plan { get; set; }

        [Attr("artifact-type-id")]
        public int? ArtifactTypeId { get; set; }
        [HasOne("artifact-type", Link.None)]
        public ArtifactType ArtifactType { get; set; }
        public int? ArtifactCategoryId { get; set; }
        [HasOne("artifact-category", Link.None)]
        public ArtifactCategory ArtifactCategory { get; set; }

        [Attr("version-number")]
        public int? VersionNumber { get; set; }

        [Attr("eaf-url")]
        public string EafUrl { get; set; }
        [Attr("audio-url")]
        public string AudioUrl { get; set; }
        [Attr("duration")]
        public int? Duration { get; set; }
        [Attr("content-type")]
        public string ContentType { get; set; }
        [Attr("audio-quality")]
        public string AudioQuality { get; set; }
        [Attr("text-quality")]
        public string TextQuality { get; set; }
        [Attr("transcription")]
        public string Transcription { get; set; }

        [Attr("original-file")]
        public string OriginalFile { get; set; }
        [Attr("s3file")]
        public string S3File { get; set; }
        [Attr("filesize")]
        public long Filesize { get; set; }
        [Attr("position")]
        public double Position { get; set; }
        [Attr("topic")]
        public string Topic { get; set; }
        [Attr("transcriptionstate")]
        public string Transcriptionstate { get; set; }
        public bool Archived { get; set; }

        [Attr("segments")]
        [Column(TypeName = "jsonb")]
        public string Segments { get; set; }
        public int? RecordedbyUserId { get; set; }
        [Attr("recordedby-user")]
        public User RecordedbyUser { get; set; }

        [Attr("languagebcp47")]
        public string Languagebcp47 { get; set; }
        
        [Attr("performed-by")]
        public string PerformedBy { get; set; }
        [Attr("ready-to-share")]
        public bool ReadyToShare { get; set; }
        [Attr("resource-passage-id")]
        public int? ResourcePassageId { get; set; }
        [HasOne("resource-passage", Link.None)]
        public Passage ResourcePassage { get; set; }
        [Attr("offline-id")]
        public string OfflineId { get; set; }
        [Attr("source-media-offline-id")]
        public string SourceMediaOfflineId { get; set; }

        [Attr("source-media-id")]
        public int? SourceMediaId { get; set; }
        [HasOne("source-media",Link.None)]
        public Mediafile SourceMedia { get; set; }

        [Attr("source-segments")]
        [Column(TypeName = "jsonb")]
        public string SourceSegments { get; set; }

        public bool ReadyToSync
        {
            get { return Transcriptionstate == "approved" && !Archived; }
        }

    }
}
