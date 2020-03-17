using JsonApiDotNetCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Models
{

    public partial class VwPassageStateHistoryEmail : BaseModel
    {
        [Attr("projectid")]
        public int ProjectId { get; set; }
        [Attr("project")]
        public string ProjectName { get; set; }

        //[Attr("description")]
        public string Description { get; set; }
        //[Attr("owner-id")]
        public int OwnerId { get; set; }

        [Attr("organization")]
        public string Organization { get; set; }
        [Attr("organizationid")]
        public int OrganizationId { get; set; }
        //[Attr("language")]
        public string Language { get; set; }
        //[Attr("group-id")]
        public int? Groupid { get; set; }

        [Attr("planid")]
        public int PlanId { get; set; }
        [Attr("plan")]
        public string Planname { get; set; }
        [Attr("plantype")]
        public string Plantype { get; set; }

        //[Attr("section-id")]
        public int SectionId { get; set; }
        //[Attr("section-name")]
        public string SectionName { get; set; }
        //[Attr("section-sequencenum")]
        public int? SectionSequencenum { get; set; }
        //[Attr("section-state")]
        public string SectionState { get; set; }
        //[Attr("transcriber-id")]
        public int? TranscriberId { get; set; }
        //[Attr("transcriber-email")]
        public string TranscriberEmail { get; set; }
        [Attr("transcriber")]
        public string Transcriber { get; set; }
        public int? EditorId { get; set; }
        //[Attr("editor-email")]
        public string EditorEmail { get; set; }
        [Attr("editor")]
        public string Editor { get; set; }


        //[Attr("passage-id")]
        public int Passageid { get; set; }
        //[Attr("passage-sequencenum")]
        public int PassageSequencenum { get; set; }
        //[Attr("book")]
        public string Book { get; set; }
        //[Attr("reference")]
        public string Reference { get; set; }

        [Attr("passage")]
        public string Passage { get; set; }
        [Attr("state")]
        public string PassageState { get; set; }


        [Attr("modifiedby")]
        public string StateModifiedby { get; set; }
        [Attr("updated")]
        public DateTime StateUpdated { get; set; }
        [Attr("email")]
        public string Email { get; set; }
        [Attr("timezone")]
        public string Timezone { get; set; }
        [Attr("locale")]
        public string Locale { get; set; }
        [Attr("comments")]
        public string Comments { get; set; }
        //[Attr("email-type")]
        // public string EmailType { get; set; }
    }
}
