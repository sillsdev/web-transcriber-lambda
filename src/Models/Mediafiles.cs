using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using Microsoft.AspNetCore.Http;

namespace SIL.Transcriber.Models
{
    public partial class Mediafile : BaseModel, ITrackDate
    {
        [Attr("passage-id")]
        public int? PassageId { get; set; }
        [HasOne("passage")]
        public virtual Passage Passage { get; set; }

        [Attr("plan-id")]
        public int PlanId { get; set; }
        [HasOne("plan")]
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

        [Attr("s3file")]
        public string S3file { get; set; }

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }

    }
}
