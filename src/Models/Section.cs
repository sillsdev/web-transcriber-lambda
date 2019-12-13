using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class Section : BaseModel, IArchive
    {
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }
        [Attr("name")]
        public string Name { get; set; }
        [Attr("state")]
        public string State { get; set; }

        [Attr("plan-id")]
        public int PlanId { get; set; }

        [HasOne("plan")]
        public virtual Plan Plan { get; set; }

        [Attr("transcriber-id")]
        public int? TranscriberId { get; set; }

        [HasOne("transcriber")]
        public virtual User Transcriber { get; set; }

        [Attr("reviewer-id")]
        public int? ReviewerId { get; set; }

        [HasOne("reviewer")]
        public virtual User Reviewer { get; set; }

        //causes errors when organization specified
        //        [NotMapped]
        //        [HasManyThrough(nameof(PassageSections))]
        //        public List<Passage> Passages { get; set; }

        [HasMany("passage-sections")]
        public List<PassageSection> PassageSections { get; set; }
        public bool Archived { get; set; }

    }
}
