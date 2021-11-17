using JsonApiDotNetCore.Models;
using System;


namespace SIL.Transcriber.Models
{

    public partial class Resource : BaseModel
    {
        public int ProjectId { get; set; }
        [Attr("project-name")]
        public string ProjectName { get; set; }

        public int OrganizationId { get; set; }
        [Attr("organization")]
        public string Organization { get; set; }
        
        [Attr("language")]
        public string Language { get; set; }

        public int PlanId { get; set; }
        [Attr("plan")]
        public string Planname { get; set; }
        [Attr("plantype")]
        public string Plantype { get; set; }

        public int SectionId { get; set; }
        [Attr("section")]
        public string SectionName { get; set; }
        [Attr("section-sequencenum")]
        public int? SectionSequencenum { get; set; }
        

        public int Passageid { get; set; }
        [Attr("passage-sequencenum")]
        public int PassageSequencenum { get; set; }
     
        [Attr("book")]
        public string Book { get; set; }
        [Attr("reference")]
        public string Reference { get; set; }
        [Attr("passage")]
        public string Passage { get; set; }
        [Attr("version-number")]
        public int? VersionNumber { get; set; }

        [Attr("audio-url")]
        public string AudioUrl { get; set; }
        [Attr("duration")]
        public int? Duration { get; set; }
        [Attr("content-type")]
        public string ContentType { get; set; }
        [Attr("transcription")]
        public string Transcription { get; set; }

        [Attr("original-file")]
        public string OriginalFile { get; set; }
        [Attr("s3file")]
        public string S3File { get; set; }
        [Attr("filesize")]
        public long Filesize { get; set; }
        public bool Archived { get; set; }

        [Attr("languagebcp47")]
        public string Languagebcp47 { get; set; }

        [Attr("category-name")]
        public string CategoryName { get; set; }
        [Attr("type-name")]
        public string TypeName { get; set; }
        [Attr("latest")]
        public bool Latest { get; set; }
    }
}
