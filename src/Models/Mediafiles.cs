using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Mediafile : BaseModel, IArchive
    {
        [Attr("passage-id")]
        public int? PassageId { get; set; }
        [HasOne("passage", Link.None)]
        public virtual Passage Passage { get; set; }

        [Attr("plan-id")]
        public int PlanId { get; set; }
        [HasOne("plan", Link.None)]
        public virtual Plan Plan { get; set; }


        [Attr("version-number")]
        public int? VersionNumber { get; set; }
        [Attr("artifact-type")]
        public string ArtifactType { get; set; }
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
        public string S3File { get; set; }
        [Attr("filesize")]
        public long Filesize { get; set; }
        [Attr("position")]
        public double Position { get; set; }

        public bool Archived { get; set; }

    }
}
