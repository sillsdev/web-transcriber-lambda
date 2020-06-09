using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class BaseModel : Identifiable<int>, ITrackDate, ILastModified
    {
        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }
        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }
        [Attr("last-modified-by")]
        public int? LastModifiedBy { get; set; }
        [Attr("last-modified-origin")]
        public string LastModifiedOrigin { get; set; }
        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }
    }


}