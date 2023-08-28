using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Passages)]
    public class Passage : BaseModel, IArchive
    {
        public Passage() : base()
        {
            Hold = false;
        }

        public Passage UpdateFrom(JToken item)
        {
            Book = item ["book"]?.ToString() ?? "";
            Reference = item ["reference"]?.ToString() ?? "";
            Title = item ["title"]?.ToString() ?? "";
            Sequencenum = int.TryParse(item ["sequencenum"]?.ToString() ?? "", out int tryint)
                ? tryint
                : 0;
            return this;
        }

        public Passage UpdateFrom(JToken item, int sectionId)
        {
            _ = UpdateFrom(item);
            SectionId = sectionId;
            return this;
        }

        [Attr(PublicName = "sequencenum")]
        public int Sequencenum { get; set; }

        [Attr(PublicName = "book")]
        public string? Book { get; set; }

        [Attr(PublicName = "reference")]
        public string? Reference { get; set; }

        [Attr(PublicName = "state")]
        public string? State { get; set; }

        [Attr(PublicName = "hold")]
        public bool Hold { get; set; }

        [Attr(PublicName = "title")]
        public string? Title { get; set; }

        [Attr(PublicName = "last-comment")]
        public string? LastComment { get; set; }

        [Attr(PublicName = "section-id")]
        [ForeignKey("Section")]
        public int SectionId { get; set; }
        
        [HasOne(PublicName = "section")]
        public virtual Section? Section { get; set; }

        [Attr(PublicName = "step-complete")]
        [Column(TypeName = "jsonb")]
        public string? StepComplete { get; set; } //json

        [Attr(PublicName = "shared-resource-id")]
        [ForeignKey("SharedResource")]
        public int SharedResourceId { get; set; }

        [HasOne(PublicName = "shared-resource")]
        public Sharedresource? SharedResource { get; set; }

        [Attr(PublicName = "start-chapter")]
        public int? StartChapter { get;  }

        [Attr(PublicName = "start-verse")]
        public int? StartVerse { get;  }

        [Attr(PublicName = "end-chapter")]
        public int? EndChapter { get; }

        [Attr(PublicName = "end-verse")]
        public int? EndVerse { get; }

        public int? PassagetypeId { get; set; }

        [HasOne(PublicName = "Passagetype")]
        public virtual Passagetype? Passagetype { get; set; }

        [Attr(PublicName = "plan-id")]
        [NotMapped]
        public int PlanId {
            get { return Section?.PlanId ?? 0; }
        }
        public bool Archived { get; set; }

        public bool ReadyToSync //backward compatibility
        {
            get { return State == "approved" && !Archived; }
        }

        public bool ValidScripture {
            get {
                return StartChapter != null && StartVerse != null && EndVerse != null;
            }
        }
       
        public string Verses {
            get {
                string? tmp = StartChapter != EndChapter
                    ? Reference
                    : StartVerse != EndVerse ? StartVerse?.ToString() + "-" + EndVerse?.ToString() : StartVerse?.ToString();
                return tmp ?? "";
            }
        }

    }
}
