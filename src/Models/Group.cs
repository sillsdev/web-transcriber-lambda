using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Group : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("abbreviation")]
        public string Abbreviation { get; set; }

        [HasOne("owner")]
        public virtual Organization Owner { get; set; }
        public int OwnerId { get; set; }
        
    }
}
