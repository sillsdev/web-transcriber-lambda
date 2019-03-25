using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Book : BaseModel
    {

        [Attr("name")]
        public string Name { get; set; }
        [Attr("book-type-id")]
        public int BookTypeId { get; set; }

        [HasOne("type")]
        public BookType Booktype { get; set; }
        [HasMany("sets")]
        public virtual List<Set> Sets { get; set; }
    }
}
