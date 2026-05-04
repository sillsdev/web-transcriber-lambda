using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Data.Tables.UserAnalytics)]
    public class Useranalytic : Identifiable<int>
    {
        [Attr(PublicName = "user-id")]
        public int UserId { get; set; }

        [Attr(PublicName = "year")]
        public int Year { get; set; }

        [Attr(PublicName = "month")]
        public int Month { get; set; }
    }
}