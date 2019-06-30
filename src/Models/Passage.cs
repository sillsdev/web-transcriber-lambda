using System;
using System.Collections.Generic;
using System.Collections;
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Passage : BaseModel, IArchive
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

       [HasMany("mediafiles")]
       public virtual List<Mediafile> Mediafiles { get; set; }

 //     [NotMapped]
 //     [HasManyThrough(nameof(PassageSections))]
 //     public List<Section> Sections { get; set; }
        [HasMany("passage-sections")]
        public List<PassageSection> PassageSections { get; set; }
        [HasMany("user-passages")]
        public List<UserPassage> UserPassages { get; set; }
        public bool Archived { get; set; }

    }
}
