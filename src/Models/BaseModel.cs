using System;
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

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [HasOne("last-modified-by-user", Link.None)]
        public User LastModifiedByUser { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int? LastModifiedByUserId
        {
            get
            {
                return LastModifiedBy;
            }
            set
            {
                LastModifiedBy = value;
            }
        }
        [Attr("last-modified-origin")]
        public string LastModifiedOrigin { get; set; }
        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }
    }


}