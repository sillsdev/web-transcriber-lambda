﻿using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class PassageSection : BaseModel, IArchive
    {
        [Attr("passage-id")]
        public int PassageId { get; set; }
        [HasOne("passage")]
        public virtual Passage Passage { get; set; }

        [Attr("section-id")]
        public int SectionId { get; set; }

        [HasOne("section")]
        public virtual Section Section { get; set; }

        public bool Archived { get; set; }
    }
}
