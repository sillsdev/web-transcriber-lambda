using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Sections)]
    public class Section : BaseModel, IArchive
    {
        public Section() : base()
        {
            Name = "";
        }

        public Section UpdateFrom(JToken item)
        {
            Name = item ["title"]?.ToString() ?? "";
            Sequencenum = decimal.TryParse(item ["sequencenum"]?.ToString() ?? "", out decimal trydec)
                ? trydec
                : 0;
            Level = int.TryParse(item ["level"]?.ToString() ?? "", out int tryint)
                ? tryint
                : 3;
            Published = bool.TryParse(item ["published"]?.ToString() ?? "false", out bool trybool)
&& trybool;
            return this;
        }

        public Section UpdateFrom(JToken item, int planId)
        {
            _ = UpdateFrom(item);
            PlanId = planId;
            return this;
        }

        [Attr(PublicName = "sequencenum")]
        public decimal Sequencenum { get; set; }

        [Attr(PublicName = "name")]
        public string Name { get; set; }

        [Attr(PublicName = "state")]
        public string? State { get; set; }

        [Attr(PublicName = "plan-id")]
        public int PlanId { get; set; }

        [EagerLoad]
        [HasOne(PublicName = "plan")]
        public virtual Plan? Plan { get; set; }

        [Attr(PublicName = "transcriber-id")]
        public int? TranscriberId { get; set; }

        [EagerLoad]
        [HasOne(PublicName = "transcriber")]
        public virtual User? Transcriber { get; set; }

        [Attr(PublicName = "editor-id")]
        public int? EditorId { get; set; }

        [EagerLoad]
        [HasOne(PublicName = "editor")]
        public virtual User? Editor { get; set; }


        public int? GroupId { get; set; }
        [EagerLoad]
        [HasOne(PublicName = "group")]
        public virtual Group? Group { get; set; }

        [Attr(PublicName = "published")]
        public bool Published { get; set; }
        [Attr(PublicName = "level")]
        public int Level { get; set; }
        [Attr(PublicName = "title-mediafile-id")]
        [ForeignKey(nameof(TitleMediafile))]
        public int? TitleMediafileId { get; set; }

        [HasOne(PublicName = "title-mediafile")]
        public Mediafile? TitleMediafile { get; set; }
        public bool Archived { get; set; }


        public string SectionHeader(bool addNumbers = true)
        {
            return (addNumbers ? Sequencenum.ToString() + " - " : "") + Name;
        }
    }
}
