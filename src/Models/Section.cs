using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class Section : BaseModel
    {
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }
        [Attr("name")]
        public string Name { get; set; }
        [Attr("state")]
        public string State { get; set; }

        [Attr("plan-id")]
        public int? PlanId { get; set; }

        [HasOne("plan")]
        public virtual Plan Plan { get; set; }

        [NotMapped]
        [HasManyThrough(nameof(PassageSections))]
        public List<Passage> Passages { get; set; }
        public List<Passagesection> PassageSections { get; set; }

    }
}
