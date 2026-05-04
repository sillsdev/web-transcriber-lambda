using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Data.Tables.CountryAnalytics)]
    public class Countryanalytic : Identifiable<int>
    {
        [Attr(PublicName = "country")]
        public string Country { get; set; } = "";

        [Attr(PublicName = "year")]
        public int Year { get; set; }

        [Attr(PublicName = "month")]
        public int Month { get; set; }
    }
}