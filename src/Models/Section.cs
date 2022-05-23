using Newtonsoft.Json.Linq;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("sections")]
    public class Section : BaseModel, IArchive
    { 
        public Section() : base ()
        {
            Name = "";
        }
        public Section UpdateFrom(JToken item)
        {
            Name = item["title"]?.ToString() ?? "";
            Sequencenum = int.TryParse(item["sequencenum"]?.ToString()??"", out int tryint) ? tryint : 0;
            return this;
        }
        public Section UpdateFrom(JToken item, int planId)
        {
            UpdateFrom(item);
            PlanId = planId;
            return this;
        }
        [Attr(PublicName="sequencenum")]
        public int Sequencenum { get; set; }
        [Attr(PublicName="name")]
        public string Name { get; set; }
        [Attr(PublicName="state")]
        public string? State { get; set; }

        [Attr(PublicName="plan-id")]
        public int PlanId { get; set; }
        [EagerLoad]
        [HasOne(PublicName="plan")]
        public virtual Plan? Plan { get; set; }

        [Attr(PublicName="transcriber-id")]
        public int? TranscriberId { get; set; }

        [EagerLoad]
        [HasOne(PublicName="transcriber")]
        public virtual User? Transcriber { get; set; }

        [Attr(PublicName="editor-id")]
        public int? EditorId { get; set; }

        [EagerLoad]
        [HasOne(PublicName="editor")]
        public virtual User? Editor { get; set; }

        [HasMany(PublicName="passages")]
        public List<Passage>? Passages { get; set; }
        public bool Archived { get; set; }

        public string SectionHeader(bool addNumbers = true) { return (addNumbers ? Sequencenum.ToString() + " - " : "") + Name; }

    }
}
