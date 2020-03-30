﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using Newtonsoft.Json.Linq;

namespace SIL.Transcriber.Models
{
    public class Section : BaseModel, IArchive
    { 
        public Section() : base ()
        { }
        public Section(JToken item) : base()
        {
            UpdateFrom(item);
        }
        public Section UpdateFrom(JToken item)
        {
            Name = item["title"] != null ? (string)item["title"] : "";
            Sequencenum = int.TryParse((string)item["sequencenum"], out int tryint) ? tryint : 0;
            PlanId = item["planid"] != null ? (int)item["planid"] : 0;
            return this;
        }
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

        [Attr("editor-id")]
        public int? EditorId { get; set; }

        [HasOne("editor")]
        public virtual User Editor { get; set; }

        [HasMany("passages")]
        public List<Passage> Passages { get; set; }
        public bool Archived { get; set; }

    }
}
