using System;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class DashboardDetail : Identifiable<string>
    {
        [NotMapped]
        [Attr("total")]
        public int Total { get; set; }
        [NotMapped]
        [Attr("month")]
        public int Month { get; set; }
        [NotMapped]
        [Attr("week")]
        public int Week { get; set; }
    }
    public partial class Dashboard : IIdentifiable<int> //GRRR forced to put this here by JSONAPI
    {
        [NotMapped]
        [Attr("projects")]
        public DashboardDetail Projects { get; set; }
        [NotMapped]
        [Attr("training")]
        public DashboardDetail Training { get; set; }
        [NotMapped]
        [Attr("plans")]
        public DashboardDetail Plans { get; set; }
        [NotMapped]
        [Attr("scripture")]
        public DashboardDetail Scripture { get; set; }
        [NotMapped]
        [Attr("passages")]
        public DashboardDetail Passages { get; set; }
        [NotMapped]
        [Attr("transcriptions")]
        public DashboardDetail Transcriptions { get; set; }
        [NotMapped]
        [Attr("paratext")]
        public DashboardDetail Paratext { get; set; }
        public int Id { get; set; }
        public string StringId { get; set; }
    }
}