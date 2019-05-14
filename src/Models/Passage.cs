using System;
using System.Collections.Generic;
using System.Collections;
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Passage : BaseModel, ITrackDate
    {
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }
        [Attr("book")]
        public string Book { get; set; }
        [Attr("reference")]
        public string Reference { get; set; }
        [Attr("position")]
        public double? Position { get; set; }
        [Attr("state")]
        public string State { get; set; }
        [Attr("hold")]
        public Boolean Hold { get; set; }
        [Attr("title")]
        public string Title { get; set; }
        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }
        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }

       [HasMany("mediafiles")]
       public virtual List<Mediafile> Mediafiles { get; set; }

        [NotMapped]
        [HasManyThrough(nameof(PassageSections))]
        public List<Section> Sections { get; set; }
        public List<PassageSection> PassageSections { get; set; }

    }
}
