using System;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("vwpassagestatehistoryemails")]
    public partial class Vwpassagestatehistoryemail : BaseModel
    {
        [Attr(PublicName="projectid")]
        public int ProjectId { get; set; }
        [Attr(PublicName="project")]
        public string? ProjectName { get; set; }

        //[Attr(PublicName="description")]
        public string? Description { get; set; }
        //[Attr(PublicName="owner-id")]
        public int OwnerId { get; set; }

        [Attr(PublicName="organization")]
        public string? Organization { get; set; }
        [Attr(PublicName="organizationid")]
        public int OrganizationId { get; set; }
        //[Attr(PublicName="language")]
        public string? Language { get; set; }
        //[Attr(PublicName="group-id")]
        public int? Groupid { get; set; }

        [Attr(PublicName="planid")]
        public int PlanId { get; set; }
        [Attr(PublicName="plan")]
        public string? Planname { get; set; }
        [Attr(PublicName="plantype")]
        public string? Plantype { get; set; }

        //[Attr(PublicName="section-id")]
        public int SectionId { get; set; }
        //[Attr(PublicName="section-name")]
        public string? SectionName { get; set; }
        //[Attr(PublicName="section-sequencenum")]
        public int? SectionSequencenum { get; set; }
        //[Attr(PublicName="section-state")]
        public string? SectionState { get; set; }
        //[Attr(PublicName="transcriber-id")]
        public int? TranscriberId { get; set; }
        //[Attr(PublicName="transcriber-email")]
        public string? TranscriberEmail { get; set; }
        [Attr(PublicName="transcriber")]
        public string? Transcriber { get; set; }
        public int? EditorId { get; set; }
        //[Attr(PublicName="editor-email")]
        public string? EditorEmail { get; set; }
        [Attr(PublicName="editor")]
        public string? Editor { get; set; }


        //[Attr(PublicName="passage-id")]
        public int Passageid { get; set; }
        //[Attr(PublicName="passage-sequencenum")]
        public int PassageSequencenum { get; set; }
        //[Attr(PublicName="book")]
        public string? Book { get; set; }
        //[Attr(PublicName="reference")]
        public string? Reference { get; set; }

        [Attr(PublicName="passage")]
        public string? Passage { get; set; }
        [Attr(PublicName="state")]
        public string? PassageState { get; set; }


        [Attr(PublicName="modifiedby")]
        public string? StateModifiedby { get; set; }
        [Attr(PublicName="updated")]
        public DateTime StateUpdated { get; set; }
        [Attr(PublicName="email")]
        public string? Email { get; set; }
        [Attr(PublicName="timezone")]
        public string? Timezone { get; set; }
        [Attr(PublicName="locale")]
        public string? Locale { get; set; }
        [Attr(PublicName="comments")]
        public string? Comments { get; set; }
    }
}
