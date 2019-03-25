using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class TaskMedia : BaseModel, ITrackDate
    {
        [Attr("task-id")]
        public int TaskId { get; set; }
        [HasOne("task")]
        public virtual Task Task { get; set; }

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

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }

    }
}
