﻿using System.Collections.Generic;
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
        public int PlanId { get; set; }

        [HasOne("plan")]
        public virtual Plan Plan { get; set; }

        //causes errors when organization specified
//        [NotMapped]
//        [HasManyThrough(nameof(PassageSections))]
//        public List<Passage> Passages { get; set; }

        [HasMany("passagesections")]
        public List<Passagesection> PassageSections { get; set; }

    }
}
