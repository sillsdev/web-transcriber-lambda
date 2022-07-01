using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class DashboardDetail : Identifiable<string>
    {
        [NotMapped]
        [Attr(PublicName = "total")]
        public int Total { get; set; }
        [NotMapped]
        [Attr(PublicName = "month")]
        public int Month { get; set; }
        [NotMapped]
        [Attr(PublicName = "week")]
        public int Week { get; set; }
    }
    public partial class Dashboard : IIdentifiable<int> //GRRR forced to put this here by JSONAPI
    {
        [NotMapped]
        [Attr(PublicName = "projects")]
        public DashboardDetail? Projects { get; set; }
        [NotMapped]
        [Attr(PublicName = "training")]
        public DashboardDetail? Training { get; set; }
        [NotMapped]
        [Attr(PublicName = "plans")]
        public DashboardDetail? Plans { get; set; }
        [NotMapped]
        [Attr(PublicName = "scripture")]
        public DashboardDetail? Scripture { get; set; }
        [NotMapped]
        [Attr(PublicName = "passages")]
        public DashboardDetail? Passages { get; set; }
        [NotMapped]
        [Attr(PublicName = "transcriptions")]
        public DashboardDetail? Transcriptions { get; set; }
        [NotMapped]
        [Attr(PublicName = "paratext")]
        public DashboardDetail? Paratext { get; set; }
        public int Id { get; set; }
        public string? StringId { get; set; }
        string? IIdentifiable.LocalId { get; set; }
    }
}