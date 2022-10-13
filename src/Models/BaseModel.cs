using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    public partial class BaseModel : Identifiable<int>, ITrackDate, ILastModified
    {
        [NotMapped]
        [Attr(PublicName = "id-list")]
        public string? IdList { get; set; }

        [Attr(PublicName = "date-created")]
        public DateTime? DateCreated { get; set; }
        [Attr(PublicName = "date-updated")]
        public DateTime? DateUpdated { get; set; }

        [Attr(PublicName = "last-modified-by")]
        public int? LastModifiedBy { get; set; }

        [HasOne(PublicName = "last-modified-by-user")]
        virtual public User? LastModifiedByUser { get; set; }

        [Attr(PublicName = "last-modified-origin")]
        public string? LastModifiedOrigin { get; set; }
        public object ShallowCopy()
        {
            return this.MemberwiseClone();
        }
    }



}
