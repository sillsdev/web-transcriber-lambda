using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;
using static SIL.Transcriber.Utility.ResourceHelpers;
using System.Text.Json;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Passages)]
    public class Passage : BaseModel, IArchive
    {
        private int? destinationChapter;
        private int? startChapterLastVerse;
        private bool destinationChapterSet = false;

        public Passage() : base()
        {
            Hold = false;
        }

        public Passage UpdateFrom(JToken item)
        {
            Book = item ["book"]?.ToString() ?? "";
            Reference = item ["reference"]?.ToString() ?? "";
            Title = item ["title"]?.ToString() ?? "";
            Sequencenum = decimal.TryParse(item ["sequencenum"]?.ToString() ?? "", out decimal trydec)
                ? trydec
                : 0;
            SharedResourceId = int.TryParse(item ["sharedresourceId"]?.ToString() ?? "", out int tryint)
                ? tryint
                : null;
            PassagetypeId = int.TryParse(item ["passagetypeId"]?.ToString() ?? "", out tryint)
                ? tryint
                : null;
            return this;
        }

        public Passage UpdateFrom(JToken item, int sectionId)
        {
            _ = UpdateFrom(item);
            SectionId = sectionId;
            return this;
        }

        [Attr(PublicName = "sequencenum")]
        public decimal Sequencenum { get; set; }

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
        public int? SharedResourceId { get; set; }

        [HasOne(PublicName = "shared-resource")]
        public Sharedresource? SharedResource { get; set; }

        [Attr(PublicName = "start-chapter")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? StartChapter { get; set; }

        [Attr(PublicName = "start-verse")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? StartVerse { get; set; }

        [Attr(PublicName = "end-chapter")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? EndChapter { get; set; }

        [Attr(PublicName = "end-verse")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] 
        public int? EndVerse { get; set; }

        [Attr(PublicName = "passagetype-id")]
        [ForeignKey("Passagetype")]
        public int? PassagetypeId { get; set; }

        [HasOne(PublicName = "passagetype")]
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
       
        private static string VerseRange(int? s, int? e)
        {
            int start = s??1;
            int end  = e??1;
            return start == end ? start.ToString() : start.ToString() + "-" + end.ToString();
        }
        public string Verses(int chapter)
        {
            return (StartChapter != EndChapter
                    ? StartChapter == chapter 
                            ? VerseRange(StartVerse, startChapterLastVerse) 
                            : VerseRange(1, EndVerse)
                    : VerseRange(StartVerse,EndVerse))
                    ?? "";
        }
        public int ChapterStartVerse(int chapter)
        {
            return (chapter == StartChapter) ? (StartVerse??1) : 1;
        }
        public int ChapterEndVerse(int chapter)
        {
            if (chapter == EndChapter || Book is null) return EndVerse??1000;
            if (!destinationChapterSet)
            {
                DestinationChapter();
            }
            return startChapterLastVerse ?? 1000;
        }

        public int? DestinationChapter()
        {
            if (!destinationChapterSet)
            {
                if (StartChapter is null || Book is null || StartChapter == EndChapter)
                {
                    destinationChapter = StartChapter;
                }
                else
                {
                    string verses = LoadResource("eng-vrs.json");
                    Dictionary<string, int []>? versemap = JsonSerializer.Deserialize<Dictionary<string, int[]>>(verses);
                    startChapterLastVerse = versemap?[Book]?[(StartChapter??1)-1] ?? 1000;
                    destinationChapter = (EndVerse > startChapterLastVerse - StartVerse + 1 ? EndChapter : StartChapter) ?? 0;
                }
                destinationChapterSet = true;
            }
            return destinationChapter;
        }


    }
}
